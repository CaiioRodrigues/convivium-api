namespace Convivium.Domain.Condominiums;

using Convivium.Domain.Common;
using Convivium.Domain.People;

/// <summary>
/// Unidade autonoma (apartamento, loja, casa). E a unidade de cobranca:
/// o rateio das despesas do mes se divide entre as unidades ativas.
/// </summary>
public class Unit : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    public Condominium Condominium { get; set; } = null!;

    public Guid? BlockId { get; set; }

    public Block? Block { get; set; }

    /// <summary>Identificacao dentro do bloco. Ex.: "101", "Loja 3".</summary>
    public string Identifier { get; set; } = string.Empty;

    public int? Floor { get; set; }

    public UnitKind Kind { get; set; } = UnitKind.Apartment;

    /// <summary>Area privativa em metros quadrados.</summary>
    public decimal? AreaM2 { get; set; }

    /// <summary>
    /// Fracao ideal: a parcela do terreno e das partes comuns que pertence a esta unidade.
    /// Expressa como fracao de 1 (0.0125 = 1,25%). A soma de todas as unidades de um
    /// condominio deve fechar em 1. E o divisor padrao do rateio e o peso do voto em assembleia.
    /// </summary>
    public decimal IdealFraction { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UnitOccupancy> Occupancies { get; set; } = [];

    /// <summary>Ex.: "Bloco A - 101" ou apenas "101" quando nao ha blocos.</summary>
    public string FullIdentifier => Block is null ? Identifier : $"{Block.Name} - {Identifier}";
}
