namespace Convivium.Application.Auth;

using System.Security.Cryptography;
using System.Text;
using Convivium.Application.Abstractions;
using Convivium.Domain.Common;
using Convivium.Domain.People;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Login, renovacao e troca de condominio ativo.
/// </summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IAccessTokenFactory tokenFactory,
    IClock clock)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string email = Normalize(request.Email);

        Person? person = await db.People
            .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

        // Verifica o hash mesmo quando a pessoa nao existe: sem isso, a diferenca
        // de tempo entre "e-mail inexistente" e "senha errada" entrega quais
        // e-mails estao cadastrados.
        bool passwordMatches = passwordHasher.Verify(
            request.Password ?? string.Empty,
            person?.PasswordHash ?? DummyHash);

        if (person is null || !passwordMatches || !person.CanSignIn)
        {
            throw new AuthenticationFailedException();
        }

        person.LastLoginAt = clock.Now;

        var accesses = await LoadAccessesAsync(person, cancellationToken);
        CondominiumAccess? active = accesses.FirstOrDefault();

        return await IssueAsync(person, accesses, active, clientIp, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        string hash = HashToken(refreshToken);

        RefreshToken? stored = await db.RefreshTokens
            .Include(t => t.Person)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw new AuthenticationFailedException("Sessao invalida.");
        }

        if (!stored.IsActive(clock.Now))
        {
            // Um token ja rotacionado sendo reapresentado significa que alguem
            // esta com uma copia antiga: derruba a cadeia inteira da pessoa.
            if (stored.ReplacedByTokenHash is not null)
            {
                await RevokeAllForPersonAsync(stored.PersonId, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException("Sessao expirada.");
        }

        Person person = stored.Person;
        if (!person.CanSignIn)
        {
            throw new AuthenticationFailedException("Acesso desativado.");
        }

        var accesses = await LoadAccessesAsync(person, cancellationToken);

        // Mantem o condominio que a sessao ja usava, se a pessoa ainda tiver acesso.
        CondominiumAccess? active =
            accesses.FirstOrDefault(a => a.Id == stored.CondominiumId) ?? accesses.FirstOrDefault();

        stored.RevokedAt = clock.Now;

        var result = await IssueAsync(person, accesses, active, clientIp, cancellationToken, stored);
        return result;
    }

    public async Task<AuthResult> SwitchCondominiumAsync(
        Guid personId,
        Guid condominiumId,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        Person person = await db.People.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new AuthenticationFailedException("Usuario nao encontrado.");

        var accesses = await LoadAccessesAsync(person, cancellationToken);

        CondominiumAccess active = accesses.FirstOrDefault(a => a.Id == condominiumId)
            ?? throw new DomainException("Voce nao tem acesso a esse condominio.");

        return await IssueAsync(person, accesses, active, clientIp, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        string hash = HashToken(refreshToken);

        RefreshToken? stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = clock.Now;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Condominios aos quais a pessoa tem acesso, do papel mais alto para o mais baixo.
    /// </summary>
    /// <remarks>
    /// Precisa de <c>IgnoreQueryFilters</c>: o login acontece antes de existir um
    /// condominio ativo, e o filtro global esconderia justamente os vinculos que
    /// definem a quais condominios a pessoa pertence.
    /// </remarks>
    private async Task<IReadOnlyList<CondominiumAccess>> LoadAccessesAsync(
        Person person,
        CancellationToken cancellationToken)
    {
        DateOnly today = clock.Today;

        if (person.IsSuperAdmin)
        {
            return await db.Condominiums
                .IgnoreQueryFilters()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CondominiumAccess(c.Id, c.Name, MembershipRole.Administrator))
                .ToListAsync(cancellationToken);
        }

        return await db.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.PersonId == person.Id)
            .Where(m => m.EndedOn == null || m.EndedOn >= today)
            .Where(m => m.Condominium.IsActive)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.Condominium.Name)
            .Select(m => new CondominiumAccess(m.CondominiumId, m.Condominium.Name, m.Role))
            .ToListAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueAsync(
        Person person,
        IReadOnlyList<CondominiumAccess> accesses,
        CondominiumAccess? active,
        string? clientIp,
        CancellationToken cancellationToken,
        RefreshToken? rotating = null)
    {
        AccessToken accessToken = tokenFactory.Create(person, active?.Id, active?.Role);

        string refreshValue = GenerateRefreshToken();
        string refreshHash = HashToken(refreshValue);

        db.RefreshTokens.Add(new RefreshToken
        {
            PersonId = person.Id,
            TokenHash = refreshHash,
            ExpiresAt = clock.Now.Add(RefreshTokenLifetime),
            CreatedByIp = clientIp,
            CondominiumId = active?.Id,
        });

        if (rotating is not null)
        {
            rotating.ReplacedByTokenHash = refreshHash;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshValue,
            new AuthenticatedPerson(person.Id, person.Name, person.Email, person.IsSuperAdmin),
            accesses,
            active?.Id,
            active?.Role);
    }

    private async Task RevokeAllForPersonAsync(Guid personId, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(t => t.PersonId == personId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in active)
        {
            token.RevokedAt = clock.Now;
        }
    }

    private static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>256 bits de entropia, em base64 seguro para URL.</summary>
    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// SHA-256 basta aqui — diferente de senha, o token ja tem entropia alta,
    /// entao nao ha o que um hash lento protegeria contra forca bruta.
    /// </summary>
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));

    /// <summary>Hash BCrypt descartavel, usado so para igualar o tempo de resposta.</summary>
    private const string DummyHash = "$2a$12$C6UzMDM.H6dfI/f/IKcEe.3CqQNmRXQ8wJ8mNM0PYyZ3Bq0YkUvJK";
}
