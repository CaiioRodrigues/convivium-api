namespace Convivium.Application.Utilities;

using Convivium.Domain.Utilities;

public sealed record UtilityBillDto(
    Guid Id,
    UtilityProvider Provider,
    string SourceFileName,
    UtilityBillStatus Status,
    decimal? Amount,
    DateOnly? DueDate,
    string? ReferenceMonth,
    string? InstallationCode,
    string? CustomerName,
    decimal? ConsumptionKwh,
    decimal? ConsumptionCubicMeters,
    string? BarcodeLine,
    IReadOnlyList<string> Warnings,
    Guid? ExpenseId,
    DateTimeOffset ImportedAt);

/// <summary>
/// Correcao manual do que o leitor nao conseguiu extrair ou extraiu errado.
/// Campos nulos mantem o valor que ja estava.
/// </summary>
public sealed record ReviewUtilityBillRequest(
    decimal? Amount = null,
    DateOnly? DueDate = null,
    string? ReferenceMonth = null,
    string? InstallationCode = null,
    decimal? ConsumptionKwh = null,
    decimal? ConsumptionCubicMeters = null);

/// <summary>Transforma a fatura lida em uma despesa do caixa.</summary>
public sealed record ConvertBillToExpenseRequest(
    Guid? LedgerAccountId = null,
    Guid? SupplierId = null,
    string? Description = null,
    bool? IsApportionable = null);

public sealed record UtilityBillFilter
{
    public UtilityProvider? Provider { get; init; }

    public UtilityBillStatus? Status { get; init; }

    public string? InstallationCode { get; init; }
}
