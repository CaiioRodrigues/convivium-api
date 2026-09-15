namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Platform;
using Convivium.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Administracao da plataforma: os condominios em si.
/// </summary>
/// <remarks>
/// Fora de "api/condominio" (singular), que e o condominio ativo da sessao.
/// Aqui o assunto e o conjunto deles, e so quem administra a plataforma entra.
/// </remarks>
[ApiController]
[Route("api/condominios")]
[Authorize(Policy = ConviviumPolicies.SuperAdmin)]
public sealed class PlatformController(
    PlatformService plataforma,
    DemoDataSeeder demonstracao) : ControllerBase
{
    /// <summary>Todos os condominios cadastrados.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CondominiumSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CondominiumSummaryDto>>> List(
        [FromQuery] bool incluirInativos = false,
        CancellationToken cancellationToken = default)
        => Ok(await plataforma.ListAsync(incluirInativos, cancellationToken));

    /// <summary>Cria o condominio e nomeia o sindico, que recebe convite de acesso.</summary>
    [HttpPost]
    [ProducesResponseType<CreateCondominiumResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateCondominiumResult>> Create(
        CreateCondominiumRequest request,
        CancellationToken cancellationToken)
    {
        CreateCondominiumResult resultado = await plataforma.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, resultado);
    }

    /// <summary>
    /// Gera o condominio de demonstracao, com seis meses de historico.
    /// </summary>
    /// <remarks>
    /// Serve para ver as telas com dados dentro sem sujar o condominio de
    /// verdade. Chamar duas vezes nao duplica nada: o seed confere se o
    /// condominio de demonstracao ja existe.
    /// </remarks>
    [HttpPost("demonstracao")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GerarDemonstracao(CancellationToken cancellationToken)
    {
        await demonstracao.SeedAsync(cancellationToken);
        return NoContent();
    }
}
