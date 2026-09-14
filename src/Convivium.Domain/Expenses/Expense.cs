namespace Convivium.Domain.Expenses;

using Convivium.Domain.Common;
using Convivium.Domain.Finance;

/// <summary>
/// Uma conta a pagar do condominio. Enquanto esta <see cref="ExpenseStatus.Pending"/>
/// ela e um compromisso; quando paga, gera um <see cref="LedgerEntry"/> de saida.
/// </summary>
public class Expense : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Conta do plano de contas que classifica a despesa (obrigatoria para os graficos).</summary>
    public Guid LedgerAccountId { get; set; }

    public LedgerAccount LedgerAccount { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    /// <summary>Mes a que a despesa se refere — e o que entra no rateio daquela competencia.</summary>
    public Competence Competence { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;

    /// <summary>
    /// Se entra no rateio do mes. Herda o padrao da conta contabil,
    /// mas pode ser ajustado caso a caso (ex.: uma obra custeada pelo fundo de reserva).
    /// </summary>
    public bool IsApportionable { get; set; } = true;

    public string? DocumentNumber { get; set; }

    public string? Notes { get; set; }

    public DateOnly? PaidOn { get; set; }

    /// <summary>Lancamento de caixa gerado no pagamento.</summary>
    public Guid? LedgerEntryId { get; set; }

    /// <summary>Fatura em PDF que originou a despesa, quando ela veio de uma importacao.</summary>
    public Guid? UtilityBillId { get; set; }

    public Guid? CreatedByPersonId { get; set; }

    public bool IsOverdue =>
        Status == ExpenseStatus.Pending && DueDate < DateOnly.FromDateTime(DateTime.UtcNow);
}
