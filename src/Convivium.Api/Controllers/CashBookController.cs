namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Common;
using Convivium.Application.Finance;
using Convivium.Domain.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>O caixa do condominio: contas, saldo, extrato e lancamentos.</summary>
[Route("api/caixa")]
[Authorize(Policy = ConviviumPolicies.Council)]
public sealed class CashBookController(CashBookService cashBook) : ApiControllerBase
{
    /// <summary>Saldo consolidado, separando caixa operacional do fundo de reserva.</summary>
    [HttpGet("posicao")]
    [ProducesResponseType<CashPosition>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashPosition>> GetPosition(CancellationToken cancellationToken)
        => Ok(await cashBook.GetPositionAsync(cancellationToken));

    /// <summary>Lista os lancamentos com filtros e paginacao.</summary>
    [HttpGet("lancamentos")]
    [ProducesResponseType<PagedResult<LedgerEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LedgerEntryDto>>> ListEntries(
        [FromQuery] LedgerEntryFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await cashBook.ListEntriesAsync(filter, page, cancellationToken));

    /// <summary>
    /// Extrato de uma conta no periodo, com o saldo anterior ao primeiro dia —
    /// o formato que permite conferir linha a linha contra o extrato do banco.
    /// </summary>
    [HttpGet("contas/{bankAccountId:guid}/extrato")]
    [ProducesResponseType<CashStatement>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashStatement>> GetStatement(
        Guid bankAccountId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
        => Ok(await cashBook.GetStatementAsync(bankAccountId, from, to, cancellationToken));

    /// <summary>Lanca uma entrada ou saida manual no caixa.</summary>
    [HttpPost("lancamentos")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<LedgerEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LedgerEntryDto>> CreateEntry(
        [FromBody] CreateLedgerEntryRequest request,
        CancellationToken cancellationToken)
    {
        LedgerEntryDto entry = await cashBook.CreateEntryAsync(request, Tenant.PersonId, cancellationToken);
        return CreatedAtAction(nameof(ListEntries), new { id = entry.Id }, entry);
    }

    /// <summary>Marca ou desmarca lancamentos como conferidos contra o extrato bancario.</summary>
    [HttpPost("lancamentos/conciliar")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType<ReconcileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconcileResponse>> Reconcile(
        [FromBody] ReconcileRequest request,
        CancellationToken cancellationToken)
    {
        int affected = await cashBook.ReconcileAsync(request.EntryIds, request.Reconciled, cancellationToken);
        return Ok(new ReconcileResponse(affected));
    }

    /// <summary>Exclui um lancamento manual que ainda nao foi conciliado.</summary>
    [HttpDelete("lancamentos/{id:guid}")]
    [Authorize(Policy = ConviviumPolicies.Finance)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken cancellationToken)
    {
        await cashBook.DeleteEntryAsync(id, cancellationToken);
        return NoContent();
    }

    // --- Contas bancarias ---

    /// <summary>Cadastra uma conta bancaria ou um caixa em especie.</summary>
    [HttpPost("contas")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<BankAccountSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BankAccountSummary>> CreateBankAccount(
        [FromBody] CreateBankAccountRequest request,
        CancellationToken cancellationToken)
    {
        BankAccountSummary account = await cashBook.CreateBankAccountAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPosition), new { id = account.Id }, account);
    }

    // --- Plano de contas ---

    /// <summary>Plano de contas em arvore.</summary>
    [HttpGet("plano-de-contas")]
    [ProducesResponseType<IReadOnlyList<LedgerAccountNode>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerAccountNode>>> GetChartOfAccounts(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await cashBook.GetChartOfAccountsAsync(includeInactive, cancellationToken));

    /// <summary>Cria uma conta no plano de contas. O pai e deduzido do codigo.</summary>
    [HttpPost("plano-de-contas")]
    [Authorize(Policy = ConviviumPolicies.Manager)]
    [ProducesResponseType<LedgerAccountNode>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LedgerAccountNode>> CreateLedgerAccount(
        [FromBody] CreateLedgerAccountRequest request,
        CancellationToken cancellationToken)
    {
        LedgerAccountNode account = await cashBook.CreateLedgerAccountAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetChartOfAccounts), new { id = account.Id }, account);
    }
}

public sealed record ReconcileRequest(IReadOnlyList<Guid> EntryIds, bool Reconciled = true);

public sealed record ReconcileResponse(int Affected);
