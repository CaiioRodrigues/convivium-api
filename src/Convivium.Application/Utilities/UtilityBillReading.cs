namespace Convivium.Application.Utilities;

using Convivium.Domain.Common;
using Convivium.Domain.Utilities;

/// <summary>
/// O que um leitor conseguiu extrair de uma fatura em PDF.
/// </summary>
/// <remarks>
/// Todo campo e opcional de proposito. Um leitor que nao achou o vencimento
/// deve devolver nulo e registrar o aviso, nunca chutar uma data — uma
/// despesa com vencimento errado gera juros de verdade.
/// </remarks>
public sealed record UtilityBillReading
{
    public UtilityProvider Provider { get; init; } = UtilityProvider.Unknown;

    public decimal? Amount { get; init; }

    public DateOnly? DueDate { get; init; }

    public Competence? ReferenceMonth { get; init; }

    /// <summary>Numero da instalacao (CEMIG) ou matricula (COPASA).</summary>
    public string? InstallationCode { get; init; }

    public string? CustomerName { get; init; }

    public decimal? ConsumptionKwh { get; init; }

    public decimal? ConsumptionCubicMeters { get; init; }

    /// <summary>Linha digitavel de arrecadacao, 48 digitos.</summary>
    public string? BarcodeLine { get; init; }

    /// <summary>O que o leitor nao conseguiu resolver sozinho.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Verdadeiro quando tudo que a despesa precisa foi extraido.</summary>
    public bool IsComplete => Amount is > 0 && DueDate is not null && ReferenceMonth is not null;
}

/// <summary>
/// Le uma fatura de um layout especifico. Cada concessionaria tem o seu:
/// a CEMIG nao imprime a conta igual a COPASA.
/// </summary>
public interface IUtilityBillParser
{
    UtilityProvider Provider { get; }

    /// <summary>Reconhece se o texto extraido pertence ao layout deste leitor.</summary>
    bool CanParse(string text);

    UtilityBillReading Parse(string text);
}

/// <summary>Extrai o texto de um PDF.</summary>
public interface IPdfTextExtractor
{
    string Extract(Stream pdf);
}
