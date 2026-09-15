namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Metering;
using Convivium.Domain.Common;
using Convivium.Domain.Metering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Folha de leitura dos medidores individuais.
/// </summary>
/// <remarks>
/// Fora de "api/cobrancas" porque a leitura acontece antes e independe do
/// rateio: mede-se o mes inteiro, e so no fechamento o resultado vira boleto.
/// </remarks>
[Route("api/medicoes")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class MeteringController(MeteringService medicoes) : ApiControllerBase
{
    /// <summary>A folha da competência, com a leitura anterior já preenchida.</summary>
    [HttpGet]
    [ProducesResponseType<MeterReadingSheet>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeterReadingSheet>> GetSheet(
        [FromQuery] string competence,
        [FromQuery] MeteredUtility utility = MeteredUtility.Gas,
        CancellationToken cancellationToken = default)
        => Ok(await medicoes.GetSheetAsync(Competence.Parse(competence), utility, cancellationToken));

    /// <summary>Grava a folha inteira, do jeito que ela é preenchida.</summary>
    [HttpPut]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<MeterReadingSheet>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MeterReadingSheet>> Save(
        [FromBody] SaveMeterReadingsRequest request,
        CancellationToken cancellationToken)
        => Ok(await medicoes.SaveAsync(request, cancellationToken));

    /// <summary>
    /// Converte o preço do cilindro em preço por metro cúbico, que é como a
    /// compra costuma ser feita: um botijão de 45 kg rende cerca de 20 m³.
    /// </summary>
    [HttpGet("preco-do-metro")]
    [ProducesResponseType<CylinderPrice>(StatusCodes.Status200OK)]
    public ActionResult<CylinderPrice> PricePerCubicMeter(
        [FromQuery] decimal cylinderCost,
        [FromQuery] decimal cubicMeters = 20m)
        => Ok(new CylinderPrice(cylinderCost, cubicMeters));
}
