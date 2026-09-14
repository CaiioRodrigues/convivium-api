namespace Convivium.Infrastructure.Services;

using Convivium.Application.Abstractions;
using Convivium.Domain.People;

/// <summary>
/// Contexto sem usuario, usado por rotinas internas: migrations, seed e os
/// servicos em segundo plano, que precisam enxergar todos os condominios.
/// </summary>
/// <remarks>
/// Nunca deve ser registrado no pipeline HTTP: la o contexto vem do token.
/// </remarks>
public sealed class SystemTenantContext : ITenantContext
{
    public Guid? CondominiumId => null;

    public Guid? PersonId => null;

    public MembershipRole? Role => MembershipRole.Administrator;

    public bool IsSuperAdmin => true;
}
