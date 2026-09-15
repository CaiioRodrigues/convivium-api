namespace Convivium.Tests.Metering;

using Convivium.Application.Metering;
using Convivium.Domain.Metering;

/// <summary>
/// A aritmetica da folha de leitura, com os numeros de uma conta de verdade:
/// cilindro de 45 kg por R$ 420,00 rendendo 20 m3, dez apartamentos medidos
/// no mesmo dia.
/// </summary>
public class MeterReadingTests
{
    private static MeterReading Leitura(decimal anterior, decimal atual, decimal preco = 21m) =>
        new() { PreviousReading = anterior, CurrentReading = atual, UnitPrice = preco };

    [Theory]
    // (anterior, atual, valor esperado) — as dez linhas da planilha do sindico.
    [InlineData(465.643, 465.644, 0.02)]
    [InlineData(68.134, 69.768, 34.31)]
    [InlineData(308.128, 309.765, 34.38)]
    [InlineData(137.412, 137.936, 11.00)]
    [InlineData(190.382, 196.415, 126.69)]
    [InlineData(97.715, 99.495, 37.38)]
    [InlineData(506.049, 508.533, 52.16)]
    [InlineData(162.086, 163.315, 25.81)]
    [InlineData(110.768, 111.952, 24.86)]
    [InlineData(427.270, 430.208, 61.70)]
    public void Cobra_o_consumo_ao_preco_do_metro_cubico(
        decimal anterior,
        decimal atual,
        decimal esperado)
    {
        Leitura(anterior, atual).Amount.ShouldBe(esperado);
    }

    [Fact]
    public void A_soma_das_dez_unidades_fecha_com_a_planilha()
    {
        (decimal Anterior, decimal Atual)[] medidas =
        [
            (465.643m, 465.644m), (68.134m, 69.768m), (308.128m, 309.765m),
            (137.412m, 137.936m), (190.382m, 196.415m), (97.715m, 99.495m),
            (506.049m, 508.533m), (162.086m, 163.315m), (110.768m, 111.952m),
            (427.270m, 430.208m),
        ];

        decimal total = medidas.Sum(m => Leitura(m.Anterior, m.Atual).Amount);

        total.ShouldBe(408.31m);
    }

    /// <summary>
    /// Medidor nao anda para tras: diferenca negativa e troca de aparelho ou
    /// digitacao errada, e nao pode virar credito no boleto de ninguem.
    /// </summary>
    [Fact]
    public void Nao_credita_quando_a_leitura_atual_e_menor()
    {
        MeterReading leitura = Leitura(anterior: 500m, atual: 12m);

        leitura.Consumption.ShouldBe(0m);
        leitura.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Arredonda_o_centavo_para_cima_no_meio_do_caminho()
    {
        // 0,5 m3 a R$ 21,01 = R$ 10,505, que precisa virar 10,51 e nao 10,50:
        // arredondar para baixo em toda unidade deixaria o condominio no
        // prejuizo pela metade de um centavo por apartamento, todo mes.
        Leitura(anterior: 10m, atual: 10.5m, preco: 21.01m).Amount.ShouldBe(10.51m);
    }

    [Theory]
    [InlineData(420, 20, 21.00)]
    [InlineData(420, 45, 9.3333)]
    [InlineData(0, 20, 0)]
    public void Converte_o_preco_do_cilindro_em_preco_do_metro(
        decimal custo,
        decimal metros,
        decimal esperado)
    {
        new CylinderPrice(custo, metros).PerCubicMeter.ShouldBe(esperado);
    }

    /// <summary>Volume zero nao explode: devolve zero e a tela pede o numero.</summary>
    [Fact]
    public void Nao_divide_por_zero_quando_o_rendimento_nao_foi_informado()
    {
        new CylinderPrice(420m, 0m).PerCubicMeter.ShouldBe(0m);
    }
}
