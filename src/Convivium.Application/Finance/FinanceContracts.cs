namespace Convivium.Application.Finance;

using Convivium.Domain.Common;
using Convivium.Domain.Finance;

// --- Contas bancarias ---

public sealed record BankAccountSummary(
    Guid Id,
    string Name,
    BankAccountKind Kind,
    string? BankCode,
    string? Agency,
    string? AccountNumber,
    bool IsReserveFund,
    bool IsActive,
    decimal OpeningBalance,
    decimal CurrentBalance);

public sealed record CreateBankAccountRequest(
    string Name,
    BankAccountKind Kind,
    string? BankCode,
    string? Agency,
    string? AccountNumber,
    decimal OpeningBalance,
    DateOnly? OpeningDate,
    bool IsReserveFund);

/// <summary>Saldo consolidado do condominio, separando o fundo de reserva.</summary>
/// <remarks>
/// O fundo aparece destacado porque tem destinacao vinculada: e dinheiro para
/// obras e emergencias, nao caixa disponivel para o custeio do mes.
/// </remarks>
public sealed record CashPosition(
    decimal TotalBalance,
    decimal OperatingBalance,
    decimal ReserveFundBalance,
    IReadOnlyList<BankAccountSummary> Accounts);

// --- Plano de contas ---

public sealed record LedgerAccountNode(
    Guid Id,
    string Code,
    string Name,
    AccountNature Nature,
    bool IsGroup,
    bool IsApportionable,
    bool IsActive,
    IReadOnlyList<LedgerAccountNode> Children);

public sealed record CreateLedgerAccountRequest(
    string Code,
    string Name,
    AccountNature Nature,
    bool IsApportionable = true,
    bool IsGroup = false);

// --- Lancamentos ---

public sealed record LedgerEntryDto(
    Guid Id,
    DateOnly Date,
    string Competence,
    string Description,
    EntryDirection Direction,
    decimal Amount,
    decimal SignedAmount,
    Guid BankAccountId,
    string BankAccountName,
    Guid LedgerAccountId,
    string LedgerAccountCode,
    string LedgerAccountName,
    string? DocumentNumber,
    bool IsReconciled,
    Guid? ExpenseId,
    Guid? PaymentId);

public sealed record CreateLedgerEntryRequest(
    Guid BankAccountId,
    Guid LedgerAccountId,
    EntryDirection Direction,
    decimal Amount,
    DateOnly Date,
    string Description,
    string? Competence = null,
    string? DocumentNumber = null);

/// <summary>
/// Extrato de um periodo, no formato que um extrato bancario tem:
/// saldo antes do periodo, movimentos e saldo ao final.
/// </summary>
public sealed record CashStatement(
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalIn,
    decimal TotalOut,
    decimal ClosingBalance,
    IReadOnlyList<LedgerEntryDto> Entries);

public sealed record LedgerEntryFilter
{
    public Guid? BankAccountId { get; init; }

    public Guid? LedgerAccountId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public EntryDirection? Direction { get; init; }

    /// <summary>Busca livre na descricao e no numero do documento.</summary>
    public string? Search { get; init; }
}
