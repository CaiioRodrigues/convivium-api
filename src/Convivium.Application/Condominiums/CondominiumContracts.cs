namespace Convivium.Application.Condominiums;

using Convivium.Domain.Billing;
using Convivium.Domain.Condominiums;

public sealed record AddressDto(
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string ZipCode);

public sealed record BillingSettingsDto(
    int DueDay,
    decimal ReserveFundRate,
    decimal LateFeeRate,
    decimal MonthlyInterestRate,
    ApportionmentMethod DefaultApportionmentMethod);

public sealed record CondominiumDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? Cnpj,
    AddressDto Address,
    BillingSettingsDto Billing,
    string? PixKey,
    PixKeyType? PixKeyType,
    string? PixReceiverName,
    string? PixReceiverCity,
    bool IsActive,
    int UnitCount,
    int ActiveUnitCount,
    decimal IdealFractionSum);

public sealed record UpdateCondominiumRequest(
    string Name,
    string? LegalName,
    string? Cnpj,
    AddressDto Address,
    BillingSettingsDto Billing,
    string? PixKey,
    PixKeyType? PixKeyType,
    string? PixReceiverName,
    string? PixReceiverCity);
