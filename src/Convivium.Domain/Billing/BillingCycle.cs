namespace Convivium.Domain.Billing;

using Convivium.Domain.Common;

/// <summary>
/// O rateio de um mes. Reune as despesas rateaveis daquela competencia,
/// divide entre as unidades e produz uma <see cref="Charge"/> para cada uma.
/// </summary>
public class BillingCycle : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Competence Competence { get; set; }

    public DateOnly DueDate { get; set; }

    public BillingCycleStatus Status { get; set; } = BillingCycleStatus.Draft;

    public ApportionmentMethod Method { get; set; } = ApportionmentMethod.IdealFraction;

    /// <summary>Soma das despesas rateaveis da competencia, congelada no fechamento.</summary>
    public decimal ApportionableTotal { get; set; }

    /// <summary>Percentual do fundo de reserva aplicado sobre a cota (0.10 = 10%).</summary>
    public decimal ReserveFundRate { get; set; }

    /// <summary>Valor total destinado ao fundo de reserva neste ciclo.</summary>
    public decimal ReserveFundTotal { get; set; }

    /// <summary>Soma de tudo que foi cobrado das unidades (rateio + fundo + itens avulsos).</summary>
    public decimal ChargedTotal { get; set; }

    /// <summary>Texto livre que aparece no boleto. Ex.: "Inclui rateio da pintura da fachada".</summary>
    public string? Notes { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? ClosedByPersonId { get; set; }

    public ICollection<Charge> Charges { get; set; } = [];

    public bool IsEditable => Status == BillingCycleStatus.Draft;
}
