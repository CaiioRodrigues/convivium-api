namespace Convivium.Tests.Utilities;

using Convivium.Application.Utilities;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;
using Convivium.Infrastructure.Utilities;

/// <summary>
/// Exercita o leitor sobre texto no formato que o PdfPig extrai de uma conta
/// da CEMIG: rotulos e valores em linhas separadas, porque o layout e em colunas.
/// </summary>
public class CemigBillParserTests
{
    private readonly CemigBillParser _parser = new();

    private const string ContaCompleta = """
        CEMIG DISTRIBUIÇÃO S.A.
        CNPJ 06.981.180/0001-16 INSC. ESTADUAL 062.322136.0087
        Av. Barbacena, 1200 - Santo Agostinho - Belo Horizonte/MG

        CONDOMINIO DO EDIFICIO RESIDENCIAL CONVIVIUM
        RUA DOS TIMBIRAS, 1420 - LOURDES
        BELO HORIZONTE - MG CEP 30140-061

        Nº DA INSTALAÇÃO                     CLASSIFICAÇÃO
        3004567890                           RESIDENCIAL TRIFASICO

        MÊS/ANO DE REFERÊNCIA          DATA DE VENCIMENTO      TOTAL A PAGAR (R$)

        AGO/2026                       15/09/2026              2.384,76

        DESCRIÇÃO DA OPERAÇÃO

        CONSUMO ATIVO                  1.632 kWh               1.412,30
        BANDEIRA TARIFÁRIA VERMELHA                              146,88
        ICMS 30,00%                                              715,43
        CIP - ILUMINAÇÃO PÚBLICA                                 110,15

        LEITURA ANTERIOR    LEITURA ATUAL     CONSUMO kWh
        45.210              46.842            1.632

        84660000023-8  84700000000-4  20260915004-2  38476000000-1
        """;

    [Fact]
    public void Reconhece_uma_conta_da_cemig()
    {
        _parser.CanParse(ContaCompleta).ShouldBeTrue();
        _parser.Provider.ShouldBe(UtilityProvider.Cemig);
    }

    [Fact]
    public void Nao_reclama_de_uma_conta_de_outra_concessionaria()
    {
        _parser.CanParse("COPASA MG - CONTA DE AGUA E ESGOTO").ShouldBeFalse();
    }

    [Fact]
    public void Le_todos_os_campos_de_uma_conta_completa()
    {
        UtilityBillReading reading = _parser.Parse(ContaCompleta);

        reading.Amount.ShouldBe(2384.76m);
        reading.DueDate.ShouldBe(new DateOnly(2026, 9, 15));
        reading.ReferenceMonth.ShouldBe(new Competence(2026, 8));
        reading.InstallationCode.ShouldBe("3004567890");
        reading.ConsumptionKwh.ShouldBe(1632m);
        reading.CustomerName.ShouldNotBeNull();
        reading.CustomerName!.ShouldContain("CONVIVIUM");
        reading.IsComplete.ShouldBeTrue();
        reading.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Extrai_a_linha_digitavel_com_48_digitos()
    {
        UtilityBillReading reading = _parser.Parse(ContaCompleta);

        reading.BarcodeLine.ShouldNotBeNull();
        reading.BarcodeLine!.Length.ShouldBe(48);
        reading.BarcodeLine.ShouldStartWith("8");
        reading.BarcodeLine.ShouldAllBe(c => char.IsAsciiDigit(c));
    }

    /// <summary>
    /// Regressao: buscar o consumo pelo rotulo "CONSUMO kWh" devolvia o
    /// primeiro numero da linha seguinte, que num layout em colunas e a
    /// leitura anterior do medidor — um acumulado dezenas de vezes maior.
    /// </summary>
    [Fact]
    public void Nao_confunde_consumo_com_a_leitura_do_medidor()
    {
        const string semConsumoEscritoComUnidade = """
            CEMIG DISTRIBUIÇÃO S.A.

            Nº DA INSTALAÇÃO
            3004567890

            MÊS/ANO DE REFERÊNCIA        DATA DE VENCIMENTO     TOTAL A PAGAR (R$)
            SET/2026                     15/10/2026             2.517,93

            LEITURA ANTERIOR    LEITURA ATUAL    CONSUMO kWh
            46.842              48.561           1.719
            """;

        UtilityBillReading reading = _parser.Parse(semConsumoEscritoComUnidade);

        reading.ConsumptionKwh.ShouldBe(1719m);
        reading.ConsumptionKwh.ShouldNotBe(46842m, "isso seria a leitura anterior do medidor");
    }

    [Fact]
    public void Deduz_o_consumo_pela_diferenca_quando_a_coluna_nao_existe()
    {
        const string semColunaDeConsumo = """
            CEMIG DISTRIBUIÇÃO S.A.

            MÊS/ANO DE REFERÊNCIA        DATA DE VENCIMENTO     TOTAL A PAGAR (R$)
            SET/2026                     15/10/2026             2.517,93

            LEITURA ANTERIOR    LEITURA ATUAL
            46.842              48.561
            """;

        _parser.Parse(semColunaDeConsumo).ConsumptionKwh.ShouldBe(1719m);
    }

    [Fact]
    public void Avisa_quando_o_consumo_declarado_diverge_das_leituras()
    {
        const string divergente = """
            CEMIG DISTRIBUIÇÃO S.A.

            MÊS/ANO DE REFERÊNCIA        DATA DE VENCIMENTO     TOTAL A PAGAR (R$)
            SET/2026                     15/10/2026             2.517,93

            LEITURA ANTERIOR    LEITURA ATUAL    CONSUMO kWh
            46.842              48.561           9.999
            """;

        UtilityBillReading reading = _parser.Parse(divergente);

        reading.Warnings.ShouldContain(w => w.Contains("diverge"));
    }

    [Fact]
    public void Avisa_o_que_nao_conseguiu_ler_em_vez_de_chutar()
    {
        const string incompleta = """
            CEMIG DISTRIBUIÇÃO S.A.
            CONDOMINIO RESIDENCIAL CONVIVIUM
            Documento sem os campos de valor e vencimento.
            """;

        UtilityBillReading reading = _parser.Parse(incompleta);

        reading.Amount.ShouldBeNull();
        reading.DueDate.ShouldBeNull();
        reading.IsComplete.ShouldBeFalse();
        reading.Warnings.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Avisa_quando_a_referencia_e_posterior_ao_vencimento()
    {
        // A conta de agosto vence em setembro, nunca o contrario.
        const string invertida = """
            CEMIG DISTRIBUIÇÃO S.A.
            MÊS/ANO DE REFERÊNCIA        DATA DE VENCIMENTO     TOTAL A PAGAR (R$)
            DEZ/2026                     15/09/2026             2.384,76
            """;

        _parser.Parse(invertida).Warnings.ShouldContain(w => w.Contains("posterior ao vencimento"));
    }

    [Theory]
    [InlineData("VALOR A PAGAR")]
    [InlineData("TOTAL A PAGAR")]
    [InlineData("TOTAL DA FATURA")]
    public void Aceita_as_variacoes_de_rotulo_do_valor(string rotulo)
    {
        string conta = $"""
            CEMIG DISTRIBUIÇÃO S.A.
            {rotulo}
            1.234,56
            """;

        _parser.Parse(conta).Amount.ShouldBe(1234.56m);
    }

    [Theory]
    [InlineData("AGO/2026", 2026, 8)]
    [InlineData("08/2026", 2026, 8)]
    [InlineData("AGOSTO/2026", 2026, 8)]
    public void Aceita_as_variacoes_de_mes_de_referencia(string texto, int ano, int mes)
    {
        string conta = $"""
            CEMIG DISTRIBUIÇÃO S.A.
            MÊS/ANO DE REFERÊNCIA
            {texto}
            """;

        _parser.Parse(conta).ReferenceMonth.ShouldBe(new Competence(ano, mes));
    }

    [Fact]
    public void Le_conta_sem_acentuacao_igual_a_com_acentuacao()
    {
        // Parte dos PDFs sai do extrator sem acento, dependendo da fonte embutida.
        string semAcento = ContaCompleta
            .Replace("Ç", "C").Replace("Ã", "A").Replace("Ê", "E")
            .Replace("Á", "A").Replace("Ú", "U").Replace("Ó", "O");

        UtilityBillReading reading = _parser.Parse(semAcento);

        reading.Amount.ShouldBe(2384.76m);
        reading.DueDate.ShouldBe(new DateOnly(2026, 9, 15));
        reading.ReferenceMonth.ShouldBe(new Competence(2026, 8));
    }
}
