namespace Convivium.Application.Platform;

using Convivium.Application.Abstractions;
using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Convivium.Domain.Finance;
using Convivium.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// <summary>
/// Administracao da plataforma: os condominios em si, acima de qualquer um deles.
/// </summary>
/// <remarks>
/// Tudo aqui atravessa o filtro multi-tenant com IgnoreQueryFilters, porque a
/// pergunta e justamente "quais condominios existem" — pergunta que nao cabe
/// dentro de um. Por isso o controller exige super admin: um sindico enxerga o
/// condominio dele e mais nada.
/// </remarks>
public sealed class PlatformService(
    IApplicationDbContext db,
    IOptions<ConviviumOptions> options,
    IClock clock)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<CondominiumSummaryDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Condominium> query = db.Condominiums.IgnoreQueryFilters().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CondominiumSummaryDto(
                c.Id,
                c.Name,
                c.Cnpj,
                c.Address.City,
                c.IsActive,
                db.Units.IgnoreQueryFilters().Count(u => u.CondominiumId == c.Id && u.IsActive),
                db.Memberships.IgnoreQueryFilters()
                    .Count(m => m.CondominiumId == c.Id && m.EndedOn == null),
                db.Memberships.IgnoreQueryFilters()
                    .Where(m => m.CondominiumId == c.Id
                        && m.EndedOn == null
                        && m.Role >= MembershipRole.Manager)
                    .Select(m => m.Person.Name)
                    .FirstOrDefault(),
                db.Memberships.IgnoreQueryFilters()
                    .Where(m => m.CondominiumId == c.Id
                        && m.EndedOn == null
                        && m.Role >= MembershipRole.Manager)
                    .Select(m => m.Person.Email)
                    .FirstOrDefault(),
                db.Memberships.IgnoreQueryFilters()
                    .Where(m => m.CondominiumId == c.Id
                        && m.EndedOn == null
                        && m.Role >= MembershipRole.Manager)
                    .Select(m => m.Person.PasswordHash == null)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cria o condominio e ja nomeia quem responde por ele.
    /// </summary>
    /// <remarks>
    /// Condominio sem sindico seria um cadastro que ninguem consegue usar, por
    /// isso os dois nascem na mesma operacao. O plano de contas padrao vem
    /// junto: sem ele o caixa e os graficos de gasto nao teriam onde pendurar
    /// lancamento nenhum.
    ///
    /// Nenhuma senha e definida aqui. O sindico recebe um convite e escolhe a
    /// propria, igual a qualquer pessoa do sistema.
    /// </remarks>
    public async Task<CreateCondominiumResult> CreateAsync(
        CreateCondominiumRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string nome = (request.Name ?? string.Empty).Trim();
        DomainException.ThrowIf(nome.Length == 0, "Informe o nome do condomínio.");

        string sindico = (request.ManagerName ?? string.Empty).Trim();
        DomainException.ThrowIf(sindico.Length == 0, "Informe o nome do síndico.");

        string? email = Normalize(request.ManagerEmail);
        DomainException.ThrowIf(
            email is null,
            "Informe o e-mail do síndico: é por ele que sai o convite de acesso.");

        string? cnpj = BrazilianDocument.OnlyDigits(request.Cnpj);
        DomainException.ThrowIf(
            cnpj is not null && !BrazilianDocument.IsValidCnpj(cnpj),
            "CNPJ inválido. Confira os números digitados.");

        bool repetido = await db.Condominiums
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Name == nome, cancellationToken);

        DomainException.ThrowIf(repetido, $"Já existe um condomínio chamado \"{nome}\".");

        // CNPJ repetido nao e detalhe de cadastro: e ele que identifica o
        // beneficiario da cobranca. Dois condominios com o mesmo numero
        // emitiriam boleto em nome de quem nao esta recebendo.
        if (cnpj is not null)
        {
            bool cnpjRepetido = await db.Condominiums
                .IgnoreQueryFilters()
                .AnyAsync(c => c.Cnpj == cnpj, cancellationToken);

            DomainException.ThrowIf(
                cnpjRepetido,
                "Já existe um condomínio com esse CNPJ.");
        }

        var condominio = new Condominium
        {
            Name = nome,
            Cnpj = cnpj,
            Address = new Address
            {
                City = (request.City ?? string.Empty).Trim(),
                State = (request.State ?? string.Empty).Trim().ToUpperInvariant(),
            },
        };

        db.Condominiums.Add(condominio);
        db.LedgerAccounts.AddRange(ChartOfAccountsTemplate.BuildFor(condominio.Id));

        // Uma pessoa que ja existe em outro condominio e reaproveitada: o login
        // dela e o mesmo, e quem administra dois predios nao quer duas contas.
        Person? pessoa = await db.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

        if (pessoa is null)
        {
            pessoa = new Person { Name = sindico, Email = email };
            db.People.Add(pessoa);
        }

        db.Memberships.Add(new Membership
        {
            CondominiumId = condominio.Id,
            PersonId = pessoa.Id,
            Role = MembershipRole.Manager,
            StartedOn = clock.Today,
        });

        // Quem ja tem senha nao precisa de convite novo — so do vinculo acima.
        string token = InviteToken.Generate();
        DateTimeOffset expiraEm = clock.Now.Add(InviteLifetime);

        if (pessoa.PasswordHash is null)
        {
            pessoa.InviteTokenHash = InviteToken.Hash(token);
            pessoa.InviteTokenExpiresAt = expiraEm;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CreateCondominiumResult(
            condominio.Id,
            condominio.Name,
            pessoa.Id,
            email!,
            pessoa.PasswordHash is null ? BuildInviteUrl(token) : string.Empty,
            expiraEm);
    }

    private string BuildInviteUrl(string token) =>
        $"{options.Value.PublicBaseUrl.TrimEnd('/')}/definir-senha/{token}";

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
