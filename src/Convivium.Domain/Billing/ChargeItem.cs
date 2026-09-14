namespace Convivium.Domain.Billing;

using Convivium.Domain.Common;
using Convivium.Domain.Finance;

/// <summary>
/// Linha do boleto. Detalhar os itens e o que permite ao morador entender
/// por que a taxa subiu de um mes para o outro.
/// </summary>
public class ChargeItem : Entity
{
    public Guid ChargeId { get; set; }

    public Charge Charge { get; set; } = null!;

    public ChargeItemKind Kind { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Positivo cobra, negativo credita (ver <see cref="ChargeItemKind.Adjustment"/>).</summary>
    public decimal Amount { get; set; }

    /// <summary>Conta contabil para onde a receita vai quando o boleto for pago.</summary>
    public Guid? LedgerAccountId { get; set; }

    public LedgerAccount? LedgerAccount { get; set; }

    /// <summary>Ordem de exibicao no boleto.</summary>
    public int Sort { get; set; }
}
