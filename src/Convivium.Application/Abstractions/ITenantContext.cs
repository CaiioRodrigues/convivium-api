namespace Convivium.Application.Abstractions;

using Convivium.Domain.Common;
using Convivium.Domain.People;

/// <summary>
/// Quem esta fazendo a requisicao e em qual condominio. Preenchido a partir
/// do token JWT no inicio de cada requisicao.
/// </summary>
public interface ITenantContext
{
    /// <summary>Condominio ativo. Nulo em rotas publicas e no login.</summary>
    Guid? CondominiumId { get; }

    Guid? PersonId { get; }

    /// <summary>Papel da pessoa no condominio ativo.</summary>
    MembershipRole? Role { get; }

    /// <summary>Operador da plataforma: enxerga todos os condominios.</summary>
    bool IsSuperAdmin { get; }

    bool HasTenant => CondominiumId is not null;

    /// <summary>Condominio ativo, ou erro se a rota exigia um e o token nao trouxe.</summary>
    Guid RequireCondominiumId() =>
        CondominiumId ?? throw new DomainException("Nenhum condominio ativo no token de acesso.");

    Guid RequirePersonId() =>
        PersonId ?? throw new DomainException("Requisicao sem usuario autenticado.");
}
