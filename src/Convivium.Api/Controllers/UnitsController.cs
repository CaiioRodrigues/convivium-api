namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Units;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Blocos e unidades do condomínio.</summary>
[Route("api/unidades")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class UnitsController(UnitService unidades) : ApiControllerBase
{
    /// <summary>
    /// Lista as unidades com a conferência da soma das frações ideais.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<UnitListDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnitListDto>> List(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
        => Ok(await unidades.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnitDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await unidades.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<UnitDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UnitDto>> Create(
        [FromBody] SaveUnitRequest request,
        CancellationToken cancellationToken)
    {
        UnitDto unidade = await unidades.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = unidade.Id }, unidade);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<UnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UnitDto>> Update(
        Guid id,
        [FromBody] SaveUnitRequest request,
        CancellationToken cancellationToken)
        => Ok(await unidades.UpdateAsync(id, request, cancellationToken));

    /// <summary>Tira a unidade do rateio, mantendo o histórico dela.</summary>
    [HttpPost("{id:guid}/desativar")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<UnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UnitDto>> Deactivate(Guid id, CancellationToken cancellationToken)
        => Ok(await unidades.DeactivateAsync(id, cancellationToken));

    /// <summary>Exclui de vez. Só funciona enquanto a unidade nunca foi cobrada.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await unidades.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Recalcula a fração ideal de todas as unidades ativas pela área
    /// privativa, fazendo a soma fechar exatamente em 1.
    /// </summary>
    [HttpPost("recalcular-fracoes")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<RedistributeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RedistributeResult>> Redistribute(CancellationToken cancellationToken)
        => Ok(await unidades.RedistributeByAreaAsync(cancellationToken));

    /// <summary>
    /// Ajusta as frações já cadastradas para somarem exatamente 1, mantendo a
    /// proporção entre elas. Use quando a convenção não fecha por
    /// arredondamento, ou quando a escala digitada ficou errada.
    /// </summary>
    [HttpPost("ajustar-fracoes")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<RedistributeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RedistributeResult>> Normalize(CancellationToken cancellationToken)
        => Ok(await unidades.NormalizeFractionsAsync(cancellationToken));

    // --- Blocos ---

    [HttpPost("blocos")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<BlockDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BlockDto>> CreateBlock(
        [FromBody] SaveBlockRequest request,
        CancellationToken cancellationToken)
    {
        BlockDto bloco = await unidades.CreateBlockAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = bloco.Id }, bloco);
    }

    [HttpDelete("blocos/{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken cancellationToken)
    {
        await unidades.DeleteBlockAsync(id, cancellationToken);
        return NoContent();
    }
}
