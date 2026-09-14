namespace Convivium.Domain.People;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// Vinculo entre uma pessoa e um condominio, com o papel que ela exerce ali.
/// A mesma pessoa pode ser sindica em um condominio e moradora em outro —
/// e por isso que o papel vive aqui, e nao em <see cref="Person"/>.
/// </summary>
public class Membership : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Condominium Condominium { get; set; } = null!;

    public Guid PersonId { get; set; }

    public Person Person { get; set; } = null!;

    public MembershipRole Role { get; set; } = MembershipRole.Resident;

    public DateOnly StartedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Fim do mandato ou da moradia. Nulo enquanto o vinculo esta ativo.</summary>
    public DateOnly? EndedOn { get; set; }

    public bool IsActive => EndedOn is null || EndedOn >= DateOnly.FromDateTime(DateTime.UtcNow);
}
