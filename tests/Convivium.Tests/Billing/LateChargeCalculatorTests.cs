namespace Convivium.Tests.Billing;

using Convivium.Domain.Billing;

/// <summary>
/// Multa e juros seguem o Codigo Civil, art. 1.336, paragrafo 1o:
/// multa de ate 2% aplicada uma vez, mais juros pro rata die.
/// </summary>
public class LateChargeCalculatorTests
{
    private static readonly DateOnly Due = new(2026, 9, 10);

    private const decimal Fine2Percent = 0.02m;
    private const decimal Interest1PercentMonth = 0.01m;

    private static LateCharge Compute(decimal outstanding, int daysLate) =>
        LateChargeCalculator.Compute(
            outstanding, Due, Due.AddDays(daysLate), Fine2Percent, Interest1PercentMonth);

    [Fact]
    public void Nao_cobra_nada_antes_do_vencimento()
    {
        Compute(1000m, daysLate: -1).ShouldBe(LateCharge.None);
    }

    [Fact]
    public void Nao_cobra_nada_no_dia_do_vencimento()
    {
        // Pagar no dia do vencimento e pagar em dia.
        Compute(1000m, daysLate: 0).Total.ShouldBe(0m);
    }

    [Fact]
    public void Aplica_multa_cheia_ja_no_primeiro_dia_de_atraso()
    {
        var late = Compute(1000m, daysLate: 1);

        // A multa nao e proporcional: incide inteira assim que atrasa.
        late.Fine.ShouldBe(20m);
        late.DaysLate.ShouldBe(1);
    }

    [Fact]
    public void Juros_sao_proporcionais_aos_dias_de_atraso()
    {
        // 1% ao mes sobre R$ 1.000 = R$ 10 em 30 dias, entao R$ 5 em 15 dias.
        Compute(1000m, daysLate: 15).Interest.ShouldBe(5m);
        Compute(1000m, daysLate: 30).Interest.ShouldBe(10m);
        Compute(1000m, daysLate: 60).Interest.ShouldBe(20m);
    }

    [Fact]
    public void Multa_nao_se_acumula_com_o_passar_dos_meses()
    {
        // So os juros crescem; a multa fica nos mesmos 2%.
        Compute(1000m, daysLate: 1).Fine.ShouldBe(20m);
        Compute(1000m, daysLate: 90).Fine.ShouldBe(20m);
    }

    [Fact]
    public void Total_soma_multa_e_juros()
    {
        var late = Compute(1153.15m, daysLate: 30);

        late.Fine.ShouldBe(23.06m);
        late.Interest.ShouldBe(11.53m);
        late.Total.ShouldBe(34.59m);
    }

    [Fact]
    public void Arredonda_para_centavos()
    {
        var late = Compute(333.33m, daysLate: 7);

        // 333,33 * 0,02 = 6,6666 -> 6,67
        late.Fine.ShouldBe(6.67m);

        // 333,33 * 0,01 * 7/30 = 0,7777... -> 0,78
        late.Interest.ShouldBe(0.78m);
    }

    [Fact]
    public void Nao_cobra_encargo_sobre_saldo_zerado()
    {
        Compute(0m, daysLate: 45).ShouldBe(LateCharge.None);
    }

    [Fact]
    public void Nao_cobra_encargo_sobre_saldo_negativo()
    {
        // Credito a favor do morador nao gera multa.
        Compute(-50m, daysLate: 45).ShouldBe(LateCharge.None);
    }

    [Fact]
    public void Respeita_taxas_diferentes_definidas_na_convencao()
    {
        var late = LateChargeCalculator.Compute(
            1000m, Due, Due.AddDays(30), lateFeeRate: 0.01m, monthlyInterestRate: 0.02m);

        late.Fine.ShouldBe(10m);
        late.Interest.ShouldBe(20m);
    }
}
