namespace Convivium.Domain.Condominiums;

using Convivium.Domain.Common;

/// <summary>Bloco ou torre. Condominios pequenos podem nao ter nenhum.</summary>
public class Block : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Condominium Condominium { get; set; } = null!;

    /// <summary>Ex.: "Bloco A", "Torre Norte".</summary>
    public string Name { get; set; } = string.Empty;

    public ICollection<Unit> Units { get; set; } = [];
}
