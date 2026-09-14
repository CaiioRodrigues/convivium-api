namespace Convivium.Domain.Finance;

using Convivium.Domain.Common;
using Convivium.Domain.Condominiums;

/// <summary>
/// Conta do plano de contas. E ela que da sentido aos graficos: sem categorizar
/// o lancamento, "saiu R$ 4.200" nao diz se foi energia, folha ou obra.
/// </summary>
/// <remarks>
/// A hierarquia e simples (pai e filho) e o <see cref="Code"/> segue o padrao
/// contabil brasileiro: "5.2" e o grupo Concessionarias, "5.2.01" e Energia Eletrica.
/// </remarks>
public class LedgerAccount : Entity, ITenantScoped
{
    public Guid CondominiumId { get; set; }

    /// <summary>Codigo hierarquico, ex.: "5.2.01".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public AccountNature Nature { get; set; }

    public Guid? ParentId { get; set; }

    public LedgerAccount? Parent { get; set; }

    public ICollection<LedgerAccount> Children { get; set; } = [];

    /// <summary>
    /// Se as despesas desta conta entram no rateio mensal dos moradores.
    /// Obras pagas pelo fundo de reserva, por exemplo, nao entram.
    /// </summary>
    public bool IsApportionable { get; set; } = true;

    /// <summary>Contas sinteticas (grupos) nao recebem lancamento direto.</summary>
    public bool IsGroup { get; set; }

    public bool IsActive { get; set; } = true;

    public string Display => $"{Code} - {Name}";
}
