namespace Convivium.Domain.Billing;

public enum ChargeItemKind
{
    /// <summary>Cota de rateio das despesas ordinarias.</summary>
    CondoFee = 1,

    ReserveFund = 2,

    /// <summary>Consumo individual medido (agua, gas).</summary>
    Metered = 3,

    /// <summary>Rateio de obra ou despesa extraordinaria.</summary>
    Extraordinary = 4,

    /// <summary>Multa por atraso de cobranca anterior.</summary>
    LateFee = 5,

    /// <summary>Juros de mora.</summary>
    Interest = 6,

    /// <summary>Multa por infracao a convencao ou ao regimento interno.</summary>
    Penalty = 7,

    /// <summary>Cobranca avulsa: reserva do salao de festas, segunda via, mudanca.</summary>
    Extra = 8,

    /// <summary>Ajuste de credito ou debito (valor pode ser negativo).</summary>
    Adjustment = 9,
}
