namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Condominiums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Dados e parâmetros de cobrança do condomínio.</summary>
[Route("api/condominio")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class CondominiumsController(CondominiumService condominios) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<CondominiumDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CondominiumDto>> Get(CancellationToken cancellationToken)
        => Ok(await condominios.GetAsync(cancellationToken));

    /// <summary>
    /// Atualiza cadastro, endereço, chave PIX e parâmetros de cobrança.
    /// </summary>
    /// <remarks>
    /// A multa por atraso é limitada a 2% conforme o Código Civil, art. 1.336,
    /// § 1º — o limite é legal, não uma escolha do produto.
    /// </remarks>
    [HttpPut]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<CondominiumDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CondominiumDto>> Update(
        [FromBody] UpdateCondominiumRequest request,
        CancellationToken cancellationToken)
        => Ok(await condominios.UpdateAsync(request, cancellationToken));
}
