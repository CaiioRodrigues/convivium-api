namespace Convivium.Application.People;

using Convivium.Domain.People;

public sealed record PersonUnitDto(
    Guid OccupancyId,
    Guid UnitId,
    string UnitIdentifier,
    OccupancyRelation Relation,
    bool IsBillingResponsible);

public sealed record PersonDto(
    Guid Id,
    string Name,
    string? Email,
    string? Cpf,
    string? Phone,
    MembershipRole Role,
    bool IsActive,
    bool CanSignIn,
    bool HasPendingInvite,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<PersonUnitDto> Units);

public sealed record SavePersonRequest(
    string Name,
    string? Email = null,
    string? Cpf = null,
    string? Phone = null,
    MembershipRole Role = MembershipRole.Resident,
    /// <summary>Vincula a uma unidade já no cadastro, evitando dois passos.</summary>
    Guid? UnitId = null,
    OccupancyRelation Relation = OccupancyRelation.Owner,
    bool IsBillingResponsible = true);

public sealed record LinkUnitRequest(
    Guid UnitId,
    OccupancyRelation Relation = OccupancyRelation.Owner,
    bool IsBillingResponsible = false);

public sealed record ChangeRoleRequest(MembershipRole Role);

/// <summary>Resultado do envio de convite de primeiro acesso.</summary>
public sealed record InviteResult(
    Guid PersonId,
    string Email,
    DateTimeOffset ExpiresAt,
    /// <summary>
    /// Link completo. Devolvido para o síndico poder repassar por outro meio
    /// quando o e-mail não chegar — o token em si não fica guardado em texto.
    /// </summary>
    string InviteUrl);

public sealed record SetPasswordRequest(string Token, string Password);

public sealed record PersonFilter
{
    public string? Search { get; init; }

    public MembershipRole? Role { get; init; }

    public bool IncludeInactive { get; init; }

    /// <summary>Apenas pessoas sem vínculo com nenhuma unidade.</summary>
    public bool? OnlyWithoutUnit { get; init; }
}
