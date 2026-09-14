namespace Convivium.Domain.Billing;

/// <summary>Encargos de atraso apurados para uma cobranca vencida.</summary>
public sealed record LateCharge(decimal Fine, decimal Interest, int DaysLate)
{
    public static readonly LateCharge None = new(0m, 0m, 0);

    public decimal Total => Fine + Interest;
}

/// <summary>
/// Calcula multa e juros de uma cobranca em atraso.
/// </summary>
/// <remarks>
/// Segue o Codigo Civil, art. 1.336, paragrafo 1o: multa de ate 2% aplicada
/// uma unica vez sobre o debito, mais juros conforme a convencao — na pratica
/// 1% ao mes, calculados pro rata die (proporcional aos dias de atraso).
/// </remarks>
public static class LateChargeCalculator
{
    private const int DaysInMonth = 30;

    public static LateCharge Compute(
        decimal outstanding,
        DateOnly dueDate,
        DateOnly referenceDate,
        decimal lateFeeRate,
        decimal monthlyInterestRate)
    {
        if (outstanding <= 0m)
        {
            return LateCharge.None;
        }

        int daysLate = referenceDate.DayNumber - dueDate.DayNumber;
        if (daysLate <= 0)
        {
            return LateCharge.None;
        }

        decimal fine = Round(outstanding * lateFeeRate);
        decimal interest = Round(outstanding * monthlyInterestRate * daysLate / DaysInMonth);

        return new LateCharge(fine, interest, daysLate);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
