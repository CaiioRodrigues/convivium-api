namespace Convivium.Domain.Finance;

using Convivium.Domain.Common;

/// <summary>
/// Um movimento no caixa. Esta e a fonte unica da verdade financeira:
/// despesas e cobrancas descrevem intencao, o lancamento registra o dinheiro
/// que de fato entrou ou saiu.
/// </summary>
public class LedgerEntry : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Guid BankAccountId { get; set; }

    public BankAccount BankAccount { get; set; } = null!;

    public Guid LedgerAccountId { get; set; }

    public LedgerAccount LedgerAccount { get; set; } = null!;

    public EntryDirection Direction { get; set; }

    /// <summary>Valor sempre positivo. O sinal vem de <see cref="Direction"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Data em que o dinheiro se movimentou (data caixa).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Mes de referencia contabil, que pode diferir da data caixa.</summary>
    public Competence Competence { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Numero da nota, do recibo ou do comprovante.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Despesa que originou a saida, quando houver.</summary>
    public Guid? ExpenseId { get; set; }

    /// <summary>Pagamento de cobranca que originou a entrada, quando houver.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Marcado quando o lancamento foi conferido contra o extrato bancario.</summary>
    public DateTimeOffset? ReconciledAt { get; set; }

    /// <summary>Quem registrou o lancamento.</summary>
    public Guid? CreatedByPersonId { get; set; }

    /// <summary>Valor com sinal: positivo para entrada, negativo para saida.</summary>
    public decimal SignedAmount => Direction == EntryDirection.In ? Amount : -Amount;
}
