namespace Convivium.Domain.Billing;

using Convivium.Domain.Common;

/// <summary>
/// Recebimento de uma cobranca. Fica separado de <see cref="Charge"/> porque
/// um morador pode pagar em parcelas ou negociar um acordo.
/// </summary>
public class Payment : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Guid ChargeId { get; set; }

    public Charge Charge { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly PaidOn { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Pix;

    /// <summary>Lancamento de entrada gerado no caixa.</summary>
    public Guid? LedgerEntryId { get; set; }

    /// <summary>ID da transacao no banco ou no PSP, para conciliacao.</summary>
    public string? ExternalId { get; set; }

    public string? Notes { get; set; }

    public Guid? RegisteredByPersonId { get; set; }
}
