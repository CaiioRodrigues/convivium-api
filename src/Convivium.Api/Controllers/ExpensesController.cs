namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Common;
using Convivium.Application.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Contas a pagar do condominio.</summary>
[Route("api/despesas")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class ExpensesController(ExpenseService expenses) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ExpenseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExpenseDto>>> List(
        [FromQuery] ExpenseFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await expenses.ListAsync(filter, page, cancellationToken));

    /// <summary>Totais de pendente, pago e vencido para os mesmos filtros da listagem.</summary>
    [HttpGet("totais")]
    [ProducesResponseType<ExpenseTotals>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpenseTotals>> Totals(
        [FromQuery] ExpenseFilter filter,
        CancellationToken cancellationToken)
        => Ok(await expenses.GetTotalsAsync(filter, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await expenses.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Create(
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ExpenseDto expense = await expenses.CreateAsync(request, Tenant.PersonId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = expense.Id }, expense);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Update(
        Guid id,
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken)
        => Ok(await expenses.UpdateAsync(id, request, cancellationToken));

    /// <summary>Da baixa na despesa e gera a saida correspondente no caixa.</summary>
    [HttpPost("{id:guid}/pagar")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Pay(
        Guid id,
        [FromBody] PayExpenseRequest request,
        CancellationToken cancellationToken)
        => Ok(await expenses.PayAsync(id, request, Tenant.PersonId, cancellationToken));

    /// <summary>Estorna o pagamento, removendo o lancamento do caixa.</summary>
    [HttpPost("{id:guid}/estornar")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Reverse(Guid id, CancellationToken cancellationToken)
        => Ok(await expenses.ReversePaymentAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpenseDto>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await expenses.CancelAsync(id, cancellationToken));
}
