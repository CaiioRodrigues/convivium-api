namespace Convivium.Infrastructure.Persistence.Seeding;

using Convivium.Application.Abstractions;
using Convivium.Domain.People;
using Convivium.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Garante que existe quem administre a plataforma na primeira subida.
/// </summary>
/// <remarks>
/// Sem isto o sistema nasce trancado: criar condominio exige super admin, e
/// criar super admin exigiria um condominio. O e-mail vem da configuracao, e
/// nao de uma migration, por dois motivos — migration e historico imutavel, e
/// a conta precisaria de uma senha, que ficaria versionada junto.
///
/// Aqui nenhuma senha e definida. A conta nasce sem senha e com um convite de
/// primeiro acesso, igual a qualquer pessoa: quem administra abre o link e
/// escolhe a propria senha. Como no primeiro boot nao ha condominio nem
/// servidor de e-mail garantido, o link sai no console.
/// </remarks>
public sealed class SuperAdminBootstrapper(
    ConviviumDbContext db,
    IOptions<ConviviumOptions> options,
    IClock clock,
    ILogger<SuperAdminBootstrapper> logger)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        string? email = Normalize(options.Value.SuperAdminEmail);

        if (email is null)
        {
            logger.LogWarning(
                "Nenhum super admin configurado. Defina App:SuperAdminEmail para poder "
                + "criar o primeiro condominio.");

            return;
        }

        Person? pessoa = await db.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

        if (pessoa is null)
        {
            pessoa = new Person
            {
                Name = "Administrador",
                Email = email,
                IsSuperAdmin = true,
            };

            db.People.Add(pessoa);
        }
        else if (!pessoa.IsSuperAdmin)
        {
            // Quem ja existia como morador e foi promovido na configuracao
            // continua sendo a mesma pessoa, com o mesmo historico.
            pessoa.IsSuperAdmin = true;
        }

        // Quem ja tem senha nao precisa de convite — e o caso de todo boot
        // depois do primeiro. Reemitir aqui invalidaria o acesso a cada
        // reinicio do servidor.
        if (pessoa.PasswordHash is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        string token = InviteToken.Generate();
        pessoa.InviteTokenHash = InviteToken.Hash(token);
        pessoa.InviteTokenExpiresAt = clock.Now.Add(InviteLifetime);

        await db.SaveChangesAsync(cancellationToken);

        string link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/definir-senha/{token}";

        logger.LogInformation(
            """

            ┌──────────────────────────────────────────────────────────────
            │ Primeiro acesso de {Email}
            │
            │ Abra este link para escolher a senha (vale {Dias} dias):
            │ {Link}
            └──────────────────────────────────────────────────────────────
            """,
            email,
            InviteLifetime.TotalDays,
            link);
    }

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
