namespace Convivium.Domain.Condominiums;

using Convivium.Domain.Billing;

/// <summary>
/// Parametros de cobranca do condominio. Owned type do <see cref="Condominium"/>.
/// </summary>
/// <remarks>
/// Os limites de multa e juros seguem o Codigo Civil, art. 1.336, paragrafo 1o:
/// multa de ate 2% sobre o debito e juros conforme a convencao (usualmente 1% ao mes).
/// </remarks>
public class BillingSettings
{
    /// <summary>Dia do mes em que a taxa condominial vence. Ex.: 10.</summary>
    public int DueDay { get; set; } = 10;

    /// <summary>
    /// Percentual do fundo de reserva sobre a taxa condominial (0.10 = 10%).
    /// Normalmente definido na convencao do condominio.
    /// </summary>
    public decimal ReserveFundRate { get; set; } = 0.10m;

    /// <summary>Multa por atraso, aplicada uma vez sobre o valor em aberto (0.02 = 2%).</summary>
    public decimal LateFeeRate { get; set; } = 0.02m;

    /// <summary>Juros de mora ao mes, cobrados pro rata die (0.01 = 1% a.m.).</summary>
    public decimal MonthlyInterestRate { get; set; } = 0.01m;

    /// <summary>Como o rateio das despesas e dividido entre as unidades.</summary>
    public ApportionmentMethod DefaultApportionmentMethod { get; set; } = ApportionmentMethod.IdealFraction;
}
