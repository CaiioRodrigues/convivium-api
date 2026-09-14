namespace Convivium.Application.Billing;

using Convivium.Domain.Billing;

// --- Previa do rateio ---

/// <summary>Quanto uma unidade pagaria com os numeros atuais.</summary>
public sealed record ApportionmentPreviewLine(
    Guid UnitId,
    string UnitIdentifier,
    decimal IdealFraction,
    decimal CondoFee,
    decimal ReserveFund,
    decimal Total,
    Guid? PayerPersonId,
    string? PayerName,
    string? PayerEmail);

/// <summary>
/// Simulacao do rateio de uma competencia, antes de fechar nada.
/// </summary>
/// <remarks>
/// Serve para o sindico conferir o valor da cota antes de congelar, e para
/// o conselho aprovar. Nada aqui e gravado.
/// </remarks>
public sealed record ApportionmentPreview(
    string Competence,
    ApportionmentMethod Method,
    decimal ApportionableTotal,
    decimal ReserveFundRate,
    decimal ReserveFundTotal,
    decimal ChargedTotal,
    int UnitCount,
    int ExpenseCount,
    IReadOnlyList<ExpenseBreakdownLine> Expenses,
    IReadOnlyList<ApportionmentPreviewLine> Units,
    IReadOnlyList<string> Warnings);

/// <summary>Uma despesa que entra no rateio, agrupada por conta contabil.</summary>
public sealed record ExpenseBreakdownLine(
    string LedgerAccountCode,
    string LedgerAccountName,
    decimal Amount,
    int Count);

// --- Ciclo ---

public sealed record BillingCycleDto(
    Guid Id,
    string Competence,
    DateOnly DueDate,
    BillingCycleStatus Status,
    ApportionmentMethod Method,
    decimal ApportionableTotal,
    decimal ReserveFundRate,
    decimal ReserveFundTotal,
    decimal ChargedTotal,
    decimal ReceivedTotal,
    int ChargeCount,
    int PaidCount,
    string? Notes,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? PublishedAt);

public sealed record OpenBillingCycleRequest(
    string Competence,
    DateOnly? DueDate = null,
    ApportionmentMethod? Method = null,
    string? Notes = null);

// --- Cobrancas ---

public sealed record ChargeItemDto(
    Guid Id,
    ChargeItemKind Kind,
    string Description,
    decimal Amount);

public sealed record ChargeDto(
    Guid Id,
    Guid UnitId,
    string UnitIdentifier,
    string Competence,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    ChargeStatus Status,
    DateOnly? PaidOn,
    Guid? PayerPersonId,
    string? PayerName,
    string? PayerEmail,
    string? PixPayload,
    string PublicToken,
    int DaysLate,
    decimal LateFee,
    decimal Interest,
    decimal TotalWithLateCharges,
    IReadOnlyList<ChargeItemDto> Items);

public sealed record ChargeFilter
{
    public string? Competence { get; init; }

    public Guid? UnitId { get; init; }

    public Guid? BillingCycleId { get; init; }

    public ChargeStatus? Status { get; init; }

    /// <summary>Apenas cobrancas vencidas e em aberto.</summary>
    public bool? OnlyOverdue { get; init; }
}

public sealed record RegisterPaymentRequest(
    decimal Amount,
    Guid BankAccountId,
    DateOnly? PaidOn = null,
    PaymentMethod Method = PaymentMethod.Pix,
    string? ExternalId = null,
    string? Notes = null);

/// <summary>Cobranca avulsa, fora do rateio: salao de festas, multa, segunda via.</summary>
public sealed record CreateExtraChargeRequest(
    Guid UnitId,
    string Description,
    decimal Amount,
    DateOnly DueDate,
    ChargeItemKind Kind = ChargeItemKind.Extra,
    string? Competence = null);

/// <summary>Resumo de inadimplencia por unidade, para o painel do sindico.</summary>
public sealed record DelinquentUnit(
    Guid UnitId,
    string UnitIdentifier,
    string? PayerName,
    string? PayerEmail,
    int OpenCharges,
    decimal OutstandingAmount,
    decimal LateCharges,
    DateOnly OldestDueDate,
    int MaxDaysLate);
