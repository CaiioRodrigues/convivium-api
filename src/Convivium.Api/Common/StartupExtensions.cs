namespace Convivium.Api.Common;

using Convivium.Infrastructure.Persistence;
using Convivium.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using Npgsql;

public static class StartupExtensions
{
    /// <summary>Quanto tempo esperar o banco aceitar conexao antes de desistir.</summary>
    private static readonly TimeSpan WaitForDatabase = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Aplica as migrations pendentes e, em desenvolvimento, popula o
    /// condominio de demonstracao.
    /// </summary>
    /// <remarks>
    /// Migrar no boot e conveniente em dev. Em producao, prefira rodar
    /// "dotnet ef database update" no deploy: duas instancias subindo ao mesmo
    /// tempo disputariam o lock de migration.
    /// </remarks>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        var db = services.GetRequiredService<ConviviumDbContext>();

        await WaitForDatabaseAsync(db, app.Logger);
        await db.Database.MigrateAsync();

        bool seedEnabled = app.Configuration.GetValue("Seed:Enabled", false);

        if (app.Environment.IsDevelopment() && seedEnabled)
        {
            var seeder = services.GetRequiredService<DemoDataSeeder>();
            await seeder.SeedAsync();
        }
    }

    /// <summary>
    /// Espera o banco aceitar conexao, tentando de novo por alguns segundos.
    /// </summary>
    /// <remarks>
    /// O Postgres do compose leva alguns segundos para aceitar conexao depois
    /// que o container sobe. Sem esta espera, subir a API junto com o banco
    /// derrubava a aplicacao na primeira tentativa e so funcionava rodando de
    /// novo — dava a impressao de que o projeto estava quebrado.
    ///
    /// A conexao e aberta direto pelo Npgsql, sem passar pelo EF: a estrategia
    /// de retry do EF tentaria por conta propria a cada volta do laco, somando
    /// segundos de espera e enchendo o log de stack trace enquanto o banco
    /// ainda esta subindo. Aqui a espera e nossa e o log sai uma linha so.
    ///
    /// Passado o prazo, joga uma mensagem que diz onde tentou conectar e o que
    /// conferir — o stack trace do Npgsql sozinho nao ajuda quem so esqueceu de
    /// subir o banco.
    /// </remarks>
    private static async Task WaitForDatabaseAsync(ConviviumDbContext db, ILogger logger)
    {
        string? connectionString = db.Database.GetConnectionString();
        DateTimeOffset limite = DateTimeOffset.UtcNow + WaitForDatabase;
        bool avisou = false;

        while (true)
        {
            try
            {
                await using var conexao = new NpgsqlConnection(connectionString);
                await conexao.OpenAsync();
                return;
            }
            catch (PostgresException erro)
            {
                // O servidor respondeu e recusou: senha errada, banco inexistente,
                // usuario sem permissao. Esperar nao conserta nenhum desses.
                throw new InvalidOperationException(Recusa(db, erro), erro);
            }
            catch (Exception erro) when (erro is NpgsqlException or SocketException)
            {
                if (DateTimeOffset.UtcNow >= limite)
                {
                    throw new InvalidOperationException(Explicacao(db, erro), erro);
                }

                if (!avisou)
                {
                    logger.LogWarning(
                        "Banco ainda nao respondeu em {Servidor}. Tentando por ate {Segundos}s...",
                        Endereco(db),
                        WaitForDatabase.TotalSeconds);

                    avisou = true;
                }

                await Task.Delay(RetryDelay);
            }
        }
    }

    /// <summary>
    /// O banco esta no ar mas recusou a conexao.
    /// </summary>
    /// <remarks>
    /// Separado da espera de proposito: aqui o servidor respondeu, entao tentar
    /// de novo por 30 segundos so atrasaria o erro sem mudar o resultado.
    /// </remarks>
    private static string Recusa(ConviviumDbContext db, PostgresException erro)
    {
        string dica = erro.SqlState switch
        {
            // invalid_password / invalid_authorization_specification
            "28P01" or "28000" =>
                "Usuario ou senha nao conferem. Se o banco ja existia de antes com outra senha, "
                + "apague o volume e suba de novo:  docker compose down -v && docker compose up -d postgres",

            // invalid_catalog_name
            "3D000" =>
                "O banco nao existe nesse servidor. Suba o do projeto:  docker compose up -d postgres",

            _ => "Confira usuario, senha e banco na connection string \"Default\", em appsettings.json.",
        };

        return $"""
        O banco em {Endereco(db)} respondeu, mas recusou a conexao.

        {dica}

        Motivo original: {erro.MessageText} (SQLSTATE {erro.SqlState})
        """;
    }

    /// <summary>
    /// O banco nao respondeu no prazo.
    /// </summary>
    /// <remarks>
    /// "Connection refused" e uma informacao forte e vale separar: significa que
    /// nada esta escutando naquela porta. Ja descarta senha errada, banco
    /// inexistente e container no ar porem quebrado — sobra "o container nao
    /// subiu". Sem essa distincao, as tres linhas de "confira" mandam procurar
    /// em lugares que o proprio erro ja eliminou.
    /// </remarks>
    private static string Explicacao(ConviviumDbContext db, Exception erro)
    {
        bool ninguemEscutando = erro is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
            || erro.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused };

        string confira = ninguemEscutando
            ? """
              Nada esta escutando nessa porta — o banco nao chegou a subir.

                docker compose up -d postgres

              Se o comando falhar, o Docker em si nao esta rodando. Se ele disser
              que a porta ja esta em uso, outro Postgres local a esta segurando:
              pare o servico ou troque a porta no docker-compose.yml e na
              connection string "Default", em appsettings.json.
              """
            : """
              Confira:
                1. O banco esta no ar?   docker compose ps postgres
                2. A porta 5432 esta livre para ele? (outro Postgres local pode estar segurando)
                3. A connection string "Default" em appsettings.json aponta para o lugar certo?
              """;

        return $"""
        Nao foi possivel conectar ao banco em {Endereco(db)} apos {WaitForDatabase.TotalSeconds:0}s.

        {confira}

        Motivo original: {erro.Message}
        """;
    }

    /// <summary>Servidor e banco da connection string, sem expor a senha.</summary>
    private static string Endereco(ConviviumDbContext db)
    {
        try
        {
            var construtor = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            return $"{construtor.Host}:{construtor.Port}/{construtor.Database}";
        }
        catch (Exception)
        {
            return "(connection string ilegivel)";
        }
    }
}
