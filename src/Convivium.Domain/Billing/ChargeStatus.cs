namespace Convivium.Domain.Billing;

public enum ChargeStatus
{
    /// <summary>Em aberto, dentro do prazo.</summary>
    Open = 1,

    /// <summary>Recebimento parcial.</summary>
    PartiallyPaid = 2,

    Paid = 3,

    /// <summary>Vencida e nao paga.</summary>
    Overdue = 4,

    Cancelled = 5,
}
