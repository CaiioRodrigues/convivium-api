namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Common;
using Convivium.Application.Expenses;
using Convivium.Application.Utilities;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Faturas de concessionaria importadas em PDF.</summary>
[Route("api/faturas")]
[Authorize(Policy = ConviviumPolicies.Finance)]
public sealed class UtilityBillsController(UtilityBillImportService bills) : ApiControllerBase
{
    /// <summary>Limite do arquivo. Uma conta de luz nao passa de alguns megabytes.</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Envia o PDF de uma fatura e le os campos automaticamente.
    /// </summary>
    /// <remarks>
    /// Importar apenas le e guarda: nada entra no caixa aqui. Gerar a despesa
    /// e um segundo passo explicito, depois de a leitura ser conferida.
    /// </remarks>
    [HttpPost("importar")]
    [RequestSizeLimit(MaxFileBytes)]
    [ProducesResponseType<UtilityBillDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UtilityBillDto>> Import(
        IFormFile file,
        [FromQuery] UtilityProvider? provider,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Envie o arquivo PDF da fatura.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new DomainException($"O arquivo excede o limite de {MaxFileBytes / 1024 / 1024} MB.");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Apenas arquivos PDF sao aceitos.");
        }

        await using Stream stream = file.OpenReadStream();

        UtilityBillDto bill = await bills.ImportAsync(
            stream, file.FileName, provider, Tenant.PersonId, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = bill.Id }, bill);
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<UtilityBillDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UtilityBillDto>>> List(
        [FromQuery] UtilityBillFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await bills.ListAsync(filter, page, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UtilityBillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UtilityBillDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await bills.GetAsync(id, cancellationToken));

    /// <summary>
    /// Texto bruto extraido do PDF. Serve para entender por que um campo nao
    /// foi encontrado e ajustar o leitor.
    /// </summary>
    [HttpGet("{id:guid}/texto")]
    [Produces("text/plain")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRawText(Guid id, CancellationToken cancellationToken)
        => Content(await bills.GetRawTextAsync(id, cancellationToken), "text/plain; charset=utf-8");

    /// <summary>Corrige manualmente o que o leitor errou ou nao encontrou.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UtilityBillDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UtilityBillDto>> Review(
        Guid id,
        [FromBody] ReviewUtilityBillRequest request,
        CancellationToken cancellationToken)
        => Ok(await bills.ReviewAsync(id, request, cancellationToken));

    /// <summary>Gera a despesa correspondente a fatura conferida.</summary>
    [HttpPost("{id:guid}/gerar-despesa")]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> ConvertToExpense(
        Guid id,
        [FromBody] ConvertBillToExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ExpenseDto expense = await bills.ConvertToExpenseAsync(
            id, request, Tenant.PersonId, cancellationToken);

        return CreatedAtAction(
            actionName: nameof(ExpensesController.Get),
            controllerName: "Expenses",
            routeValues: new { id = expense.Id },
            value: expense);
    }
}
