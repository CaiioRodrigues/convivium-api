namespace Convivium.Application.Expenses;

using Convivium.Domain.Expenses;

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    int ExpenseCount,
    /// <summary>
    /// Vai na listagem porque a tela de edicao parte dela: sem este campo, a
    /// tela devolveria o fornecedor sem as observacoes e a gravacao as apagaria.
    /// </summary>
    string? Notes = null);

public sealed record SaveSupplierRequest(
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes);

public sealed record ExpenseDto(
    Guid Id,
    string Description,
    string Competence,
    DateOnly DueDate,
    decimal Amount,
    ExpenseStatus Status,
    bool IsApportionable,
    bool IsOverdue,
    DateOnly? PaidOn,
    Guid LedgerAccountId,
    string LedgerAccountCode,
    string LedgerAccountName,
    Guid? SupplierId,
    string? SupplierName,
    string? DocumentNumber,
    string? Notes,
    Guid? UtilityBillId);

public sealed record CreateExpenseRequest(
    string Description,
    Guid LedgerAccountId,
    decimal Amount,
    DateOnly DueDate,
    string? Competence = null,
    Guid? SupplierId = null,
    bool? IsApportionable = null,
    string? DocumentNumber = null,
    string? Notes = null);

/// <summary>
/// Dados da baixa de uma despesa. O lancamento de saida no caixa e criado
/// a partir daqui, entao a conta bancaria e obrigatoria.
/// </summary>
public sealed record PayExpenseRequest(
    Guid BankAccountId,
    DateOnly? PaidOn = null,
    decimal? Amount = null,
    string? DocumentNumber = null);

public sealed record ExpenseFilter
{
    public string? Competence { get; init; }

    public ExpenseStatus? Status { get; init; }

    public Guid? LedgerAccountId { get; init; }

    public Guid? SupplierId { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }

    /// <summary>Apenas despesas vencidas e ainda em aberto.</summary>
    public bool? OnlyOverdue { get; init; }

    public string? Search { get; init; }
}

/// <summary>Totais do periodo consultado, para o cabecalho da tela de despesas.</summary>
public sealed record ExpenseTotals(
    decimal Pending,
    decimal Paid,
    decimal Overdue,
    int Count);
