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

        83690000023-0  84763004567-6  89020260915-6  00000000000-0
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

    /// <summary>
    /// A conta atual da CEMIG (NF3e), em que <b>nenhum cabecalho de coluna
    /// sobrevive a extracao</b>: o PDF entrega os valores sem nunca dizer
    /// "Referente a", "Vencimento", "Total a Pagar" nem "Leitura Anterior".
    /// </summary>
    /// <remarks>
    /// Estrutura copiada de uma conta de verdade; nome, endereco, unidade
    /// consumidora e codigo de barras sao ficticios, porque a conta original
    /// esta no nome de uma pessoa fisica.
    /// </remarks>
    private const string ContaNf3eSemRotulos = """
        DOCUMENTO AUXILIAR DA NOTA FISCAL DE ENERGIA ELÉTRICA ELETRÔNICA
        CEMIG DISTRIBUIÇÃO S.A. CNPJ 06.981.180/0001-16 / INSC. ESTADUAL 062.322136.0087.
        AV. BARBACENA, 1200 - 17° ANDAR - ALA 1 - BAIRRO SANTO AGOSTINHO

        CONDOMINIO RESIDENCIAL CONVIVIUM
        RUA DOS TIMBIRAS, 1420 - LOURDES
        30140-061 BELO HORIZONTE, MG

        3.004.567.890-12

        SET/2026                     17/10/2026                        266,06
        NOTA FISCAL Nº 429049775 - SÉRIE 000
        Data de emissão:14/09/2026

        Residencial                    Residencial                    Convencional B1

        Bifásico                                      13/08     14/09     32     12/10
        Energia kWh          PPB212308067          9.494          9.695          1          201
        Energia Elétrica            kWh     201     1,17263918     235,69     7,92     235,69
        Contrib Ilum Publica Municipal                                          30,37
        TOTAL                                                                  266,06
        Bandeira Amarela - Já Incluído no valor a pagar                           4,80

        008152383678          3.004.567.890-12          17/10/2026     R$266,06
        Setembro/2026
        83610000002-2   66063004567-3   89020260915-6   00000000000-0
        """;

    [Fact]
    public void Le_a_conta_em_que_nenhum_rotulo_sobrevive_a_extracao()
    {
        UtilityBillReading reading = _parser.Parse(ContaNf3eSemRotulos);

        reading.Amount.ShouldBe(266.06m);
        reading.DueDate.ShouldBe(new DateOnly(2026, 10, 17));
        reading.ReferenceMonth.ShouldBe(new Competence(2026, 9));
        reading.InstallationCode.ShouldBe("300456789012");
        reading.ConsumptionKwh.ShouldBe(201m);
        reading.Warnings.ShouldBeEmpty();
    }

    /// <summary>
    /// Regressao de dinheiro: "Bandeira Amarela - ja incluido no valor a pagar
    /// 4,80" casava com o rotulo "VALOR A PAGAR", e a conta de R$ 266,06 era
    /// lancada como R$ 4,80 — sem aviso, porque do ponto de vista do leitor o
    /// rotulo tinha casado.
    /// </summary>
    [Fact]
    public void Nao_confunde_a_bandeira_tarifaria_com_o_total_da_conta()
    {
        UtilityBillReading reading = _parser.Parse(ContaNf3eSemRotulos);

        reading.Amount.ShouldNotBe(4.80m, "isso e o acrescimo da bandeira, nao o total");
        reading.Amount.ShouldBe(266.06m);
    }

    /// <summary>
    /// Regressao: o codigo do medidor ("PPB212308067") tem digitos colados em
    /// letras, e eles entravam na leitura das colunas como se fossem numeros —
    /// o consumo saia da constante de multiplicacao, valendo 1 kWh.
    /// </summary>
    [Fact]
    public void Ignora_os_digitos_colados_no_codigo_do_medidor()
    {
        UtilityBillReading reading = _parser.Parse(ContaNf3eSemRotulos);

        reading.ConsumptionKwh.ShouldBe(201m);
        reading.ConsumptionKwh.ShouldNotBe(1m, "isso e a constante de multiplicacao");
    }

    [Fact]
    public void Nao_toma_a_classe_tarifaria_por_nome_do_cliente()
    {
        string? nome = _parser.Parse(ContaNf3eSemRotulos).CustomerName;

        nome.ShouldNotBeNull();
        nome!.ShouldContain("CONVIVIUM");
        nome.ShouldNotContain("CONVENCIONAL", Case.Insensitive);
    }

    /// <summary>
    /// O codigo de barras tem digito verificador e posicao fixa; o texto
    /// depende de o extrator ter ordenado as colunas direito. Discordando, o
    /// barras vence — e a divergencia precisa aparecer para quem confere.
    /// </summary>
    [Fact]
    public void Prefere_o_codigo_de_barras_quando_o_texto_discorda()
    {
        const string textoMenteOBarrasNao = """
            CEMIG DISTRIBUIÇÃO S.A.
            TOTAL A PAGAR (R$)
            9.999,99
            83610000002-2   66063004567-3   89020260915-6   00000000000-0
            """;

        UtilityBillReading reading = _parser.Parse(textoMenteOBarrasNao);

        reading.Amount.ShouldBe(266.06m);
        reading.Warnings.ShouldContain(w => w.Contains("código de barras"));
    }

    [Fact]
    public void Ignora_o_campo_de_valor_quando_ele_e_referencia_e_nao_dinheiro()
    {
        // Terceira posicao 7 ou 9 significa "valor de referencia", que nao e
        // dinheiro; ler como se fosse cobraria um numero inventado do morador.
        const string comValorDeReferencia = """
            CEMIG DISTRIBUIÇÃO S.A.
            TOTAL A PAGAR (R$)
            2.384,76
            83790000023-0   84763004567-6   89020260915-6   00000000000-0
            """;

        UtilityBillReading reading = _parser.Parse(comValorDeReferencia);

        reading.Amount.ShouldBe(2384.76m, "cai no texto, ja que o barras nao carrega dinheiro");
        reading.Warnings.ShouldNotContain(w => w.Contains("código de barras"));
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
