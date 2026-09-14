namespace Convivium.Domain.People;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// Liga uma pessoa a uma unidade. Uma unidade costuma ter um proprietario e,
/// quando alugada, tambem um inquilino — as duas linhas coexistem.
/// </summary>
public class UnitOccupancy : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public Guid PersonId { get; set; }

    public Person Person { get; set; } = null!;

    public OccupancyRelation Relation { get; set; } = OccupancyRelation.Owner;

    /// <summary>
    /// Quem recebe a cobranca desta unidade. Exatamente uma ocupacao ativa
    /// por unidade deve estar marcada; o servico de cobranca valida isso.
    /// </summary>
    public bool IsBillingResponsible { get; set; }

    public DateOnly StartedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? EndedOn { get; set; }

    public bool IsActive => EndedOn is null || EndedOn >= DateOnly.FromDateTime(DateTime.UtcNow);
}
