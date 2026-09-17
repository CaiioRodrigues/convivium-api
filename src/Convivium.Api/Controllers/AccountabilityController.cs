namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Accountability;
using Convivium.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Balancete mensal: o que o sindico leva para a assembleia.
/// </summary>
/// <remarks>
/// Aberto ao conselho para cima. Conselho fiscal existe justamente para
/// conferir isto, e nao faria sentido pedir o documento ao sindico que ele
/// deveria estar auditando.
/// </remarks>
[Route("api/prestacao-de-contas")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class AccountabilityController(AccountabilityService contas) : ApiControllerBase
{
    /// <summary>Balancete de uma competencia, em JSON.</summary>
    [HttpGet]
    [ProducesResponseType<MonthlyStatement>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MonthlyStatement>> Get(
        [FromQuery] string competence,
        CancellationToken cancellationToken)
        => Ok(await contas.GetMonthlyAsync(Competence.Parse(competence), cancellationToken));

    /// <summary>O mesmo balancete em PDF, para imprimir e distribuir.</summary>
    [HttpGet("pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPdf(
        [FromQuery] string competence,
        [FromServices] IStatementRenderer renderer,
        CancellationToken cancellationToken)
    {
        MonthlyStatement statement = await contas.GetMonthlyAsync(
            Competence.Parse(competence), cancellationToken);

        // Barra vira hifen: "09/2026" quebraria o nome do arquivo baixado.
        string nome = $"prestacao-de-contas-{statement.Competence.Replace('/', '-')}.pdf";

        return File(renderer.Render(statement), "application/pdf", nome);
    }
}
