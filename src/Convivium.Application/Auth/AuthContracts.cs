namespace Convivium.Application.Auth;

using Convivium.Domain.People;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>Um condominio ao qual a pessoa tem acesso, com o papel dela nele.</summary>
public sealed record CondominiumAccess(Guid Id, string Name, MembershipRole Role);

public sealed record AuthenticatedPerson(
    Guid Id,
    string Name,
    string? Email,
    bool IsSuperAdmin);

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    AuthenticatedPerson Person,
    IReadOnlyList<CondominiumAccess> Condominiums,
    Guid? ActiveCondominiumId,
    MembershipRole? ActiveRole);

/// <summary>Token emitido, com o instante de expiracao ja calculado.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Assina o token de acesso. Fica atras de uma interface para que a camada de
/// aplicacao nao dependa da biblioteca de JWT.
/// </summary>
public interface IAccessTokenFactory
{
    AccessToken Create(
        Person person,
        Guid? condominiumId,
        MembershipRole? role);
}
