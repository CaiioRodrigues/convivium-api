namespace Convivium.Infrastructure.Utilities;

using System.Text.RegularExpressions;
using Convivium.Application.Utilities;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;

/// <summary>
/// Le a fatura de energia da CEMIG Distribuicao.
/// </summary>
/// <remarks>
/// A conta da CEMIG segue sempre o mesmo desenho, mas o texto que sai do PDF
/// varia conforme a versao do arquivo e a posicao das colunas. Por isso cada
/// campo tem varios padroes candidatos, tentados do mais especifico para o
/// mais generico, e o que nao for encontrado vira aviso em vez de chute:
/// uma despesa lancada com vencimento errado gera juros de verdade.
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

        decimal? amount = FindAmount(normalized);
        if (amount is null)
        {
            warnings.Add("Não foi possível identificar o valor total da fatura.");
        }

        DateOnly? dueDate = FindDueDate(normalized);
        if (dueDate is null)
        {
            warnings.Add("Não foi possível identificar a data de vencimento.");
        }

        Competence? reference = FindReference(normalized);
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
            InstallationCode = FindInstallation(normalized),
            CustomerName = FindCustomerName(normalized),
            ConsumptionKwh = consumption,
            BarcodeLine = BrazilianText.ExtractBarcodeLine(normalized),
            Warnings = warnings,
        };
    }

    /// <summary>
    /// O valor a pagar. Os rotulos vao do mais especifico para o mais
    /// generico; "TOTAL" sozinho e o ultimo recurso porque tambem aparece
    /// em subtotais do detalhamento de tributos.
    /// </summary>
    private static decimal? FindAmount(string text)
    {
        string[] labels =
        [
            @"TOTAL\s+A\s+PAGAR",
            @"VALOR\s+A\s+PAGAR",
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

    private static DateOnly? FindDueDate(string text)
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

        return null;
    }

    private static Competence? FindReference(string text)
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

        return null;
    }

    private static string? FindInstallation(string text)
    {
        string[] labels =
        [
            @"N?O?\.?\s*DA\s+INSTALACAO",
            @"INSTALACAO",
            @"CODIGO\s+DO\s+CLIENTE",
        ];

        foreach (string label in labels)
        {
            if (BrazilianText.ValueAfter(text, label, InstallationPattern) is { Length: > 0 } raw)
            {
                return raw;
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
    /// colunas sao, nessa ordem, leitura anterior, leitura atual e consumo.
    /// </summary>
    private static (decimal? Previous, decimal? Current, decimal? Consumption) FindMeterRow(string text)
    {
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("LEITURA ANTERIOR", StringComparison.Ordinal))
            {
                continue;
            }

            // Os valores caem logo abaixo do cabecalho, as vezes com uma
            // linha em branco no meio por causa do espacamento do PDF.
            for (int j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
            {
                var numbers = NumberToken()
                    .Matches(lines[j])
                    .Select(m => BrazilianText.ParseMoney(m.Value))
                    .Where(v => v is > 0)
                    .Select(v => v!.Value)
                    .ToList();

                if (numbers.Count >= 2)
                {
                    return (numbers[0], numbers[1], numbers.Count >= 3 ? numbers[2] : null);
                }
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
    /// Nome do titular. Na conta da CEMIG ele aparece logo apos o bloco de
    /// identificacao da distribuidora, em caixa alta.
    /// </summary>
    private static string? FindCustomerName(string text)
    {
        var match = CustomerCandidate().Match(text);

        if (!match.Success)
        {
            return null;
        }

        string name = match.Groups["name"].Value.Trim();
        return name.Length is >= 6 and <= 200 ? name : null;
    }

    [GeneratedRegex(
        @"\d{1,3}(?:\.\d{3})*(?:,\d+)?",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex NumberToken();

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
