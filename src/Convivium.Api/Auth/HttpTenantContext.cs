namespace Convivium.Api.Auth;

using System.Security.Claims;
using Convivium.Application.Abstractions;
using Convivium.Application.Auth;
using Convivium.Domain.People;

/// <summary>
/// Le o condominio ativo e o papel direto das claims do token da requisicao.
/// </summary>
/// <remarks>
/// Vem do token, e nao da query string ou de um header, justamente para que o
/// cliente nao consiga escolher em qual condominio quer enxergar. Trocar de
/// condominio exige emitir um token novo, que so e emitido se o vinculo existir.
/// </remarks>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? CondominiumId => ReadGuid(ConviviumClaims.Condominium);

    public Guid? PersonId => ReadGuid(ConviviumClaims.Subject);

    public MembershipRole? Role =>
        Enum.TryParse(Read(ConviviumClaims.Role), out MembershipRole role) ? role : null;

    public bool IsSuperAdmin => Read(ConviviumClaims.SuperAdmin) == "1";

    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    private string? Read(string claimType) => User?.FindFirst(claimType)?.Value;

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Read(claimType), out Guid value) ? value : null;
}
