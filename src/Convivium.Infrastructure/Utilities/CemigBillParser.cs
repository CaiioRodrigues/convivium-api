namespace Convivium.Infrastructure.Utilities;

using System.Text.RegularExpressions;
using Convivium.Application.Utilities;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;

/// <summary>
/// Le a fatura de energia da CEMIG Distribuicao.
/// </summary>
/// <remarks>
/// A conta da CEMIG segue sempre o mesmo desenho no papel, mas o texto que
/// sai do PDF varia muito conforme a versao do arquivo. Nas contas atuais
/// (NF3e), <b>os cabecalhos das colunas nao sobrevivem a extracao</b>: o
/// arquivo entrega "SET/2026 17/10/2026 266,06" sem nunca dizer "Referente a",
/// "Vencimento" ou "Total a Pagar". Procurar so por rotulo devolve nada — ou,
/// pior, devolve o numero errado.
///
/// Por isso a ordem e: primeiro o que e conferido por digito verificador (o
/// codigo de barras), depois posicao de linha, e so entao rotulo. O que nao
/// for encontrado vira aviso em vez de chute: uma despesa lancada com o valor
/// errado e rateada entre os moradores.
/// </remarks>
public sealed partial class CemigBillParser : IUtilityBillParser
{
    private const string MoneyPattern = @"R?\$?\s*\d{1,3}(?:\.\d{3})*,\d{2}";
    private const string DatePattern = @"\d{2}/\d{2}/\d{2,4}";
    private const string ReferencePattern = @"(?:[A-Z]{3,9}|\d{1,2})\s*/\s*\d{2,4}";
    private const string InstallationPattern = @"\d{7,12}";
    private const string NumberPattern = @"\d{1,3}(?:\.\d{3})*(?:,\d+)?";

    public UtilityProvider Provider => UtilityProvider.Cemig;

    public bool CanParse(string text)
    {
        string normalized = BrazilianText.Normalize(text);

        return normalized.Contains("CEMIG", StringComparison.Ordinal)
            || normalized.Contains("06.981.180/0001-16", StringComparison.Ordinal)
            || normalized.Contains("06981180000116", StringComparison.Ordinal);
    }

    public UtilityBillReading Parse(string text)
    {
        string normalized = BrazilianText.Normalize(text);
        var warnings = new List<string>();

        string? barcode = BrazilianText.ExtractBarcodeLine(normalized);

        decimal? amount = FindAmount(normalized, barcode, warnings);
        if (amount is null)
        {
            warnings.Add("Não foi possível identificar o valor total da fatura.");
        }

        // As linhas que carregam o total sao a ancora do resto: numa fatura em
        // colunas, vencimento, referencia e unidade consumidora caem na mesma
        // linha do valor, e essa linha existe mesmo sem cabecalho nenhum.
        var linhasDoTotal = LinhasDoTotal(normalized, amount);

        DateOnly? dueDate = FindDueDate(normalized, linhasDoTotal);
        if (dueDate is null)
        {
            warnings.Add("Não foi possível identificar a data de vencimento.");
        }

        Competence? reference = FindReference(normalized, linhasDoTotal);
        if (reference is null)
        {
            warnings.Add("Não foi possível identificar o mês de referência.");
        }
        else if (dueDate is { } due && reference.Value > Competence.From(due))
        {
            // A conta de agosto vence em setembro, nunca o contrario.
            warnings.Add(
                $"Mes de referencia ({reference}) e posterior ao vencimento ({due:dd/MM/yyyy}). " +
                "Confira antes de lancar.");
        }

        decimal? consumption = FindConsumption(normalized, warnings);
        if (consumption is null)
        {
            warnings.Add("Não foi possível identificar o consumo em kWh.");
        }

        return new UtilityBillReading
        {
            Provider = UtilityProvider.Cemig,
            Amount = amount,
            DueDate = dueDate,
            ReferenceMonth = reference,
            InstallationCode = FindInstallation(normalized, linhasDoTotal),
            CustomerName = FindCustomerName(normalized),
            ConsumptionKwh = consumption,
            BarcodeLine = barcode,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// O valor a pagar, preferindo o codigo de barras ao texto.
    /// </summary>
    /// <remarks>
    /// O codigo de barras carrega o valor em posicao fixa e com digito
    /// verificador; o texto carrega o que o extrator conseguiu ordenar. Quando
    /// os dois existem e discordam, o barras vence e a divergencia vira aviso —
    /// e sinal de que o layout mudou e o resto da leitura merece conferencia.
    /// </remarks>
    private static decimal? FindAmount(string text, string? barcode, List<string> warnings)
    {
        decimal? doBarras = BrazilianText.AmountFromBarcode(barcode);
        decimal? doTexto = FindAmountByLabel(text);

        if (doBarras is null)
        {
            return doTexto;
        }

        if (doTexto is { } escrito && Math.Abs(escrito - doBarras.Value) > 0.01m)
        {
            warnings.Add(
                $"O texto da conta diz {escrito:N2} e o código de barras diz {doBarras:N2}. " +
                "Usei o código de barras, que tem dígito verificador — confira antes de lançar.");
        }

        return doBarras;
    }

    /// <summary>
    /// O valor a pagar escrito por extenso, achado pelo rotulo. Os rotulos vao
    /// do mais especifico para o mais generico; "TOTAL" sozinho e o ultimo
    /// recurso porque tambem aparece em subtotais do detalhamento de tributos.
    /// </summary>
    private static decimal? FindAmountByLabel(string text)
    {
        string[] labels =
        [
            @"TOTAL\s+A\s+PAGAR",

            // O "NO" excluido e o da observacao "Bandeira Amarela - ja incluido
            // no valor a pagar 4,80", que fala da bandeira e nao do total. Sem
            // essa exclusao o leitor lancava R$ 4,80 no lugar de R$ 266,06 —
            // sem aviso nenhum, porque do ponto de vista dele o rotulo casou.
            @"(?<!\bNO\s)VALOR\s+A\s+PAGAR",

            @"VALOR\s+TOTAL",
            @"TOTAL\s+DA\s+FATURA",
        ];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, MoneyPattern) is { } raw
                && BrazilianText.ParseMoney(raw) is > 0 and var value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// As linhas onde o total da conta aparece impresso.
    /// </summary>
    /// <remarks>
    /// Numa fatura desenhada em colunas, a linha do total e tambem a linha do
    /// vencimento e da referencia — tanto no cabecalho ("SET/2026 17/10/2026
    /// 266,06") quanto no canhoto de pagamento ("... 17/10/2026 R$266,06").
    /// Achar o valor primeiro e usa-lo de ancora custa uma varredura e dispensa
    /// rotulos que o PDF pode nao ter.
    /// </remarks>
    private static IReadOnlyList<string> LinhasDoTotal(string text, decimal? amount)
    {
        if (amount is not { } value)
        {
            return [];
        }

        string impresso = value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        return text.Split('\n')
            .Where(linha => linha.Contains(impresso, StringComparison.Ordinal))
            .ToList();
    }

    private static DateOnly? FindDueDate(string text, IReadOnlyList<string> linhasDoTotal)
    {
        string[] labels =
        [
            @"DATA\s+DE\s+VENCIMENTO",
            @"VENCIMENTO",
            @"VENCE\s+EM",
            @"PAGAVEL\s+ATE",
        ];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, DatePattern) is { } raw
                && BrazilianText.ParseDate(raw) is { } date)
            {
                return date;
            }
        }

        foreach (string linha in linhasDoTotal)
        {
            foreach (Match match in DateToken().Matches(linha))
            {
                if (BrazilianText.ParseDate(match.Value) is { } date)
                {
                    return date;
                }
            }
        }

        return null;
    }

    private static Competence? FindReference(string text, IReadOnlyList<string> linhasDoTotal)
    {
        string[] labels =
        [
            @"MES\s*/?\s*ANO\s+(?:DE\s+)?REFERENCIA",
            @"REFERENTE\s+A",
            @"REFERENCIA",
            @"COMPETENCIA",
        ];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, ReferencePattern) is { } raw
                && BrazilianText.ParseMonthReference(raw) is { } competence)
            {
                return competence;
            }
        }

        foreach (string linha in linhasDoTotal)
        {
            foreach (Match match in ReferenceToken().Matches(linha))
            {
                // "17/10" tambem casa com o padrao de referencia, e por isso a
                // validacao do mes decide: 17 nao e mes, a data segue adiante.
                if (BrazilianText.ParseMonthReference(match.Value) is { } competence)
                {
                    return competence;
                }
            }
        }

        return null;
    }

    private static string? FindInstallation(string text, IReadOnlyList<string> linhasDoTotal)
    {
        string[] labels =
        [
            @"N?O?\.?\s*DA\s+INSTALACAO",
            @"INSTALACAO",
            @"UNIDADE\s+CONSUMIDORA",
            @"CODIGO\s+DO\s+CLIENTE",
        ];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, InstallationPattern) is { Length: > 0 } raw)
            {
                return raw;
            }
        }

        // A CEMIG imprime a unidade consumidora pontuada, como 6.311.824.018-27.
        // Guardamos so os digitos para o campo nao ter dois formatos conforme o
        // caminho que o achou.
        foreach (string linha in linhasDoTotal)
        {
            if (ConsumerUnit().Match(linha) is { Success: true } match)
            {
                return new string(match.Value.Where(char.IsAsciiDigit).ToArray());
            }
        }

        return null;
    }

    /// <summary>
    /// Consumo faturado em kWh.
    /// </summary>
    /// <remarks>
    /// A fonte preferida e a linha do medidor, lida por posicao de coluna.
    /// Buscar pelo rotulo "CONSUMO kWh" nao funciona: num layout em colunas o
    /// valor que vem depois do rotulo na ordem de leitura e o primeiro numero
    /// da linha seguinte, que e a leitura anterior do medidor — um numero
    /// acumulado, dezenas de vezes maior que o consumo do mes.
    /// </remarks>
    private static decimal? FindConsumption(string text, List<string> warnings)
    {
        (decimal? previous, decimal? current, decimal? metered) = FindMeterRow(text);

        decimal? fromReadings = previous is { } p && current is { } c && c > p ? c - p : null;
        decimal? labelled = FindConsumptionByLabel(text);

        // Descarta o valor do rotulo quando ele e, na verdade, uma das
        // leituras do medidor — sinal de que o layout e em colunas.
        if (labelled is { } candidate
            && (candidate == previous || candidate == current))
        {
            labelled = null;
        }

        decimal? consumption = metered ?? labelled ?? fromReadings;

        if (consumption is null)
        {
            return null;
        }

        // Duas fontes independentes discordando e sinal de leitura errada.
        if (fromReadings is { } expected
            && Math.Abs(consumption.Value - expected) > 1m)
        {
            warnings.Add(
                $"Consumo lido ({consumption:N0} kWh) diverge da diferenca entre as leituras " +
                $"({expected:N0} kWh). Confira antes de lancar.");
        }

        return consumption > 0 ? consumption : null;
    }

    /// <summary>
    /// Le a linha de leituras do medidor por posicao: na conta da CEMIG as
    /// primeiras duas colunas numericas sao leitura anterior e leitura atual.
    /// </summary>
    /// <remarks>
    /// A ancora pode ser o cabecalho "Leitura Anterior", quando ele sobrevive
    /// a extracao, ou a propria linha do medidor, que comeca com "Energia kWh".
    /// Entre as colunas que vem depois das leituras, a do consumo faturado e a
    /// que fecha com a diferenca entre elas; as outras sao constante de
    /// multiplicacao e afins, e pegar a errada devolveria "1 kWh".
    /// </remarks>
    private static (decimal? Previous, decimal? Current, decimal? Consumption) FindMeterRow(string text)
    {
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            bool cabecalho = lines[i].Contains("LEITURA ANTERIOR", StringComparison.Ordinal);
            bool linhaDoMedidor = MeterRowStart().IsMatch(lines[i]);

            if (!cabecalho && !linhaDoMedidor)
            {
                continue;
            }

            // Com cabecalho, os valores caem nas linhas seguintes — as vezes com
            // uma linha em branco no meio, por causa do espacamento do PDF.
            int inicio = linhaDoMedidor ? i : i + 1;
            int fim = linhaDoMedidor ? i + 1 : Math.Min(i + 4, lines.Length);

            for (int j = inicio; j < fim; j++)
            {
                var numbers = MeterNumberToken()
                    .Matches(lines[j])
                    .Select(m => BrazilianText.ParseMoney(m.Value))
                    .Where(v => v is > 0)
                    .Select(v => v!.Value)
                    .ToList();

                if (numbers.Count < 2)
                {
                    continue;
                }

                decimal anterior = numbers[0];
                decimal atual = numbers[1];
                decimal diferenca = atual - anterior;

                decimal? consumo = numbers
                    .Skip(2)
                    .Cast<decimal?>()
                    .FirstOrDefault(n => Math.Abs(n!.Value - diferenca) <= 1m)
                    ?? (numbers.Count >= 3 ? numbers[^1] : null);

                return (anterior, atual, consumo);
            }
        }

        return (null, null, null);
    }

    /// <summary>Consumo escrito junto da unidade, como "1.632 kWh".</summary>
    private static decimal? FindConsumptionByLabel(string text)
    {
        var withUnit = ConsumptionWithUnit().Match(text);
        if (withUnit.Success && BrazilianText.ParseMoney(withUnit.Groups["value"].Value) is > 0 and var value)
        {
            return value;
        }

        string[] labels = [@"CONSUMO\s+FATURADO", @"CONSUMO\s+MEDIDO"];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, NumberPattern) is { } raw
                && BrazilianText.ParseMoney(raw) is > 0 and var fromLabel)
            {
                return fromLabel;
            }
        }

        return null;
    }

    /// <summary>
    /// Nome do titular, quando a conta esta em nome do condominio.
    /// </summary>
    /// <remarks>
    /// "Residencial" tambem e o nome da classe tarifaria, e a linha de
    /// classificacao ("Residencial Residencial Convencional B1") casava com o
    /// padrao e virava nome do cliente. Uma linha de tabela e descartada pelas
    /// palavras que so aparecem nela.
    /// </remarks>
    private static string? FindCustomerName(string text)
    {
        string[] palavrasDeTabela =
            ["CONVENCIONAL", "MONOFASICO", "BIFASICO", "TRIFASICO", "HOROSAZONAL", "SUBCLASSE"];

        foreach (Match match in CustomerCandidate().Matches(text))
        {
            string name = match.Groups["name"].Value.Trim();

            if (name.Length is < 6 or > 200)
            {
                continue;
            }

            if (palavrasDeTabela.Any(p => name.Contains(p, StringComparison.Ordinal)))
            {
                continue;
            }

            return name;
        }

        return null;
    }

    [GeneratedRegex(
        @"(?<!\d)\d{2}/\d{2}/\d{2,4}(?!\d)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex DateToken();

    [GeneratedRegex(
        @"(?:[A-Z]{3,9}|\d{1,2})\s*/\s*\d{2,4}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex ReferenceToken();

    /// <summary>A unidade consumidora da CEMIG, como 6.311.824.018-27.</summary>
    [GeneratedRegex(
        @"\d\.\d{3}\.\d{3}\.\d{3}-\d{2}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex ConsumerUnit();

    [GeneratedRegex(
        @"^\s*ENERGIA\s+KWH\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex MeterRowStart();

    // Numero isolado: o que estiver colado em letra e codigo de medidor
    // ("PPB212308067"), nao coluna de leitura.
    [GeneratedRegex(
        @"(?<![A-Z0-9.,])\d{1,3}(?:\.\d{3})*(?:,\d+)?(?![A-Z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex MeterNumberToken();

    [GeneratedRegex(
        @"(?<value>\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*KWH",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex ConsumptionWithUnit();

    [GeneratedRegex(
        @"(?<name>(?:CONDOMINIO|COND\.|EDIFICIO|RESIDENCIAL)[^\r\n]{3,90})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex CustomerCandidate();
}
