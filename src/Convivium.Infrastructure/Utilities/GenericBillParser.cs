namespace Convivium.Infrastructure.Utilities;

using Convivium.Application.Utilities;
using Convivium.Domain.Common;
using Convivium.Domain.Utilities;

/// <summary>
/// Leitor de ultimo recurso, para faturas de concessionarias sem leitor proprio.
/// </summary>
/// <remarks>
/// Procura apenas os rotulos que praticamente toda fatura brasileira usa:
/// vencimento, total a pagar e a linha digitavel. Sempre marca a fatura para
/// revisao manual, porque sem conhecer o layout nao ha como ter confianca no
/// que foi extraido.
/// </remarks>
public sealed class GenericBillParser : IUtilityBillParser
{
    private const string MoneyPattern = @"R?\$?\s*\d{1,3}(?:\.\d{3})*,\d{2}";
    private const string DatePattern = @"\d{2}/\d{2}/\d{2,4}";
    private const string ReferencePattern = @"(?:[A-Z]{3,9}|\d{1,2})\s*/\s*\d{2,4}";

    public UtilityProvider Provider => UtilityProvider.Unknown;

    /// <summary>Aceita qualquer texto: e o fallback do registro de leitores.</summary>
    public bool CanParse(string text) => !string.IsNullOrWhiteSpace(text);

    public UtilityBillReading Parse(string text)
    {
        string normalized = BrazilianText.Normalize(text);

        decimal? amount = BrazilianText.ParseMoney(
            BrazilianText.ValueAfter(normalized, @"(?:TOTAL|VALOR)\s+A\s+PAGAR", MoneyPattern));

        DateOnly? dueDate = BrazilianText.ParseDate(
            BrazilianText.ValueAfter(normalized, @"VENCIMENTO", DatePattern));

        Competence? reference = BrazilianText.ParseMonthReference(
            BrazilianText.ValueAfter(normalized, @"(?:REFERENCIA|COMPETENCIA)", ReferencePattern))
            ?? (dueDate is { } due ? Competence.From(due).Previous() : null);

        var warnings = new List<string>
        {
            "Layout de fatura não reconhecido. Confira todos os campos antes de lançar a despesa.",
        };

        if (amount is null)
        {
            warnings.Add("Valor total não identificado.");
        }

        if (dueDate is null)
        {
            warnings.Add("Data de vencimento não identificada.");
        }

        return new UtilityBillReading
        {
            Provider = UtilityProvider.Unknown,
            Amount = amount,
            DueDate = dueDate,
            ReferenceMonth = reference,
            BarcodeLine = BrazilianText.ExtractBarcodeLine(normalized),
            Warnings = warnings,
        };
    }
}
