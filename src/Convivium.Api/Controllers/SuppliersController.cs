namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Fornecedores e concessionarias.</summary>
[Route("api/fornecedores")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class SuppliersController(SupplierService suppliers) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SupplierDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> List(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await suppliers.ListAsync(search, includeInactive, cancellationToken));

    [HttpPost]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<SupplierDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SupplierDto>> Create(
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        SupplierDto supplier = await suppliers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<SupplierDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierDto>> Update(
        Guid id,
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
        => Ok(await suppliers.UpdateAsync(id, request, cancellationToken));

    /// <summary>Desativa o fornecedor sem apagar o historico de despesas dele.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await suppliers.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
