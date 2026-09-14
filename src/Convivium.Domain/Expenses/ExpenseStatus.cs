namespace Convivium.Domain.Expenses;

public enum ExpenseStatus
{
    /// <summary>Lancada e aguardando pagamento.</summary>
    Pending = 1,

    /// <summary>Paga: existe um lancamento de saida no caixa correspondente.</summary>
    Paid = 2,

    /// <summary>Cancelada. Fica no historico mas nao conta em nenhum total.</summary>
    Cancelled = 3,
}
