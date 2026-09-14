namespace Convivium.Domain.Billing;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;
using Convivium.Domain.People;

/// <summary>
/// A cobranca de uma unidade em uma competencia — o "boleto do condominio".
/// </summary>
public class Charge : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    /// <summary>Ciclo que gerou a cobranca. Nulo em cobrancas avulsas fora do rateio.</summary>
    public Guid? BillingCycleId { get; set; }

    public BillingCycle? BillingCycle { get; set; }

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    /// <summary>Quem recebe e paga: proprietario ou inquilino, conforme a ocupacao responsavel.</summary>
    public Guid? PayerPersonId { get; set; }

    public Person? Payer { get; set; }

    public Competence Competence { get; set; }

    public DateOnly DueDate { get; set; }

    /// <summary>Soma dos itens, congelada na geracao.</summary>
    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public ChargeStatus Status { get; set; } = ChargeStatus.Open;

    public DateOnly? PaidOn { get; set; }

    /// <summary>
    /// Token opaco que da acesso ao boleto sem login, para colar no e-mail.
    /// Aleatorio e de uso unico por cobranca.
    /// </summary>
    public string PublicToken { get; set; } = string.Empty;

    /// <summary>Codigo copia-e-cola do PIX (BR Code EMV), gerado no fechamento.</summary>
    public string? PixPayload { get; set; }

    /// <summary>
    /// Identificador da cobranca no provedor bancario, quando houver integracao
    /// de boleto registrado. Fica nulo enquanto a cobranca for so PIX.
    /// </summary>
    public string? ExternalSlipId { get; set; }

    /// <summary>Linha digitavel do boleto bancario, quando emitido por um banco.</summary>
    public string? BarcodeLine { get; set; }

    public DateTimeOffset? NotifiedAt { get; set; }

    public ICollection<ChargeItem> Items { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];

    public decimal OutstandingAmount => Math.Max(0m, TotalAmount - PaidAmount);

    public bool IsSettled => Status is ChargeStatus.Paid or ChargeStatus.Cancelled;
}
