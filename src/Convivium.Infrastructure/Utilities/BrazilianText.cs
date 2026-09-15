namespace Convivium.Infrastructure.Utilities;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Convivium.Domain.Common;

/// <summary>
/// Conversoes de texto de documento brasileiro para tipos do dominio.
/// </summary>
/// <remarks>
/// O texto sai do PDF do jeito que a concessionaria imprimiu: com acento ou
/// sem, "R$ 2.384,76" ou "2384,76", "AGO/2026" ou "08/2026". Estes metodos
/// absorvem essa variacao para os leitores nao precisarem repeti-la.
/// </remarks>
internal static partial class BrazilianText
{
    private static readonly string[] MonthAbbreviations =
        ["JAN", "FEV", "MAR", "ABR", "MAI", "JUN", "JUL", "AGO", "SET", "OUT", "NOV", "DEZ"];

    private static readonly string[] MonthNames =
    [
        "JANEIRO", "FEVEREIRO", "MARCO", "ABRIL", "MAIO", "JUNHO",
        "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO",
    ];

    /// <summary>
    /// Deixa o texto em caixa alta e sem acento, preservando as quebras de
    /// linha. Assim um unico padrao casa com "INSTALAÇÃO" e "INSTALACAO".
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string decomposed = text.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        // Espacos horizontais colapsam; quebras de linha ficam, porque a
        // posicao da linha e um sinal util para separar rotulo de valor.
        return HorizontalSpace().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    /// <summary>"2.384,76" ou "R$ 2384,76" viram 2384.76.</summary>
    public static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string cleaned = new(value.Where(c => char.IsAsciiDigit(c) || c is '.' or ',' or '-').ToArray());

        if (cleaned.Length == 0)
        {
            return null;
        }

        // No formato brasileiro o ponto separa milhar e a virgula separa
        // decimal — exatamente o contrario do invariante.
        cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : null;
    }

    /// <summary>"15/09/2026" ou "15/09/26" viram uma data.</summary>
    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "dd-MM-yyyy", "yyyy-MM-dd"];

        return DateTime.TryParseExact(
            value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;
    }

    /// <summary>"AGO/2026", "08/2026" e "AGOSTO/2026" viram a competencia 08/2026.</summary>
    public static Competence? ParseMonthReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = Normalize(value).Trim();
        var match = MonthReference().Match(normalized);

        if (!match.Success)
        {
            return null;
        }

        string monthToken = match.Groups["month"].Value;
        string yearToken = match.Groups["year"].Value;

        int month = ResolveMonth(monthToken);
        if (month == 0)
        {
            return null;
        }

        if (!int.TryParse(yearToken, out int year))
        {
            return null;
        }

        // Ano com dois digitos: 26 -> 2026. Faturas sao sempre deste seculo.
        if (year < 100)
        {
            year += 2000;
        }

        return year is >= 2000 and <= 2999 ? new Competence(year, month) : null;
    }

    private static int ResolveMonth(string token)
    {
        if (int.TryParse(token, out int numeric))
        {
            return numeric is >= 1 and <= 12 ? numeric : 0;
        }

        int index = Array.IndexOf(MonthNames, token);
        if (index >= 0)
        {
            return index + 1;
        }

        index = Array.IndexOf(MonthAbbreviations, token.Length > 3 ? token[..3] : token);
        return index + 1;
    }

    /// <summary>
    /// Extrai a linha digitavel de arrecadacao: 48 digitos que comecam com 8
    /// (convenio de concessionaria), impressos em quatro blocos.
    /// </summary>
    public static string? ExtractBarcodeLine(string text)
    {
        foreach (Match match in BarcodeCandidate().Matches(text))
        {
            string digits = new(match.Value.Where(char.IsAsciiDigit).ToArray());

            if (digits.Length == 48 && digits[0] == '8')
            {
                return digits;
            }
        }

        return null;
    }

    /// <summary>
    /// Valor embutido na linha digitavel de arrecadacao, em reais.
    /// </summary>
    /// <remarks>
    /// O codigo de barras e a unica parte da conta que nao depende de como a
    /// concessionaria desenhou o papel: sao 44 digitos com posicoes fixas
    /// definidas pela Febraban, e o valor mora nas posicoes 5 a 15, em centavos.
    /// Num PDF onde nenhum cabecalho de coluna sobrevive a extracao, ele e a
    /// unica fonte confiavel do total.
    ///
    /// A terceira posicao diz o que o campo guarda: 6 e 8 sao valor em dinheiro,
    /// 7 e 9 sao "valor de referencia", que nao e dinheiro nenhum. Ler um como
    /// se fosse o outro lancaria no caixa um numero inventado, entao os dois
    /// ultimos devolvem nulo em vez de chutar.
    /// </remarks>
    public static decimal? AmountFromBarcode(string? digitableLine)
    {
        string digits = new((digitableLine ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        // Impressa, a linha vem em quatro blocos de 11 digitos com um
        // verificador cada; o codigo de barras sao os 44 sem os verificadores.
        if (digits.Length == 48)
        {
            digits = string.Concat(digits[0..11], digits[12..23], digits[24..35], digits[36..47]);
        }

        if (digits.Length != 44 || digits[0] != '8' || digits[2] is not ('6' or '8'))
        {
            return null;
        }

        return long.TryParse(digits[4..15], out long centavos) && centavos > 0
            ? centavos / 100m
            : null;
    }

    /// <summary>
    /// Procura o valor que vem depois de um rotulo. Aceita quebra de linha
    /// entre os dois, porque o extrator de PDF separa rotulo e valor quando
    /// eles estao em colunas diferentes.
    /// </summary>
    public static string? ValueAfter(string text, string labelPattern, string valuePattern)
    {
        var regex = new Regex(
            $@"{labelPattern}[^\S\r\n]*[:\r\n ]{{0,4}}[\s\S]{{0,40}}?({valuePattern})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));

        Match match = regex.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"[^\S\r\n]+")]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(@"(?<month>[A-Z]{3,9}|\d{1,2})\s*[/\-]\s*(?<year>\d{2,4})")]
    private static partial Regex MonthReference();

    // Sem \s de proposito: ele casa quebra de linha, e o match atravessava
    // varias linhas juntando digitos de blocos diferentes ate estourar a
    // contagem de 48. A linha digitavel cabe sempre em uma unica linha.
    [GeneratedRegex(@"\d[\d .\-]{45,70}\d")]
    private static partial Regex BarcodeCandidate();
}
