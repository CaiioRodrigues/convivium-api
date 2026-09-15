namespace Convivium.Application.Units;

using Convivium.Domain.Condominiums;
using Convivium.Domain.People;

public sealed record BlockDto(Guid Id, string Name, int UnitCount);

public sealed record SaveBlockRequest(string Name);

public sealed record UnitOccupantDto(
    Guid OccupancyId,
    Guid PersonId,
    string Name,
    string? Email,
    OccupancyRelation Relation,
    bool IsBillingResponsible);

public sealed record UnitDto(
    Guid Id,
    Guid? BlockId,
    string? BlockName,
    string Identifier,
    string FullIdentifier,
    int? Floor,
    UnitKind Kind,
    decimal? AreaM2,
    decimal IdealFraction,
    bool IsActive,
    IReadOnlyList<UnitOccupantDto> Occupants,
    int OpenChargeCount,
    decimal OutstandingAmount);

/// <summary>
/// Lista de unidades com a conferência da fração ideal.
/// </summary>
/// <remarks>
/// A soma vem junto de propósito: fração ideal que não fecha em 1 faz o
/// rateio cobrar a mais ou a menos de todo mundo, e o erro só apareceria
/// no fechamento do mês.
/// </remarks>
public sealed record UnitListDto(
    IReadOnlyList<UnitDto> Units,
    IReadOnlyList<BlockDto> Blocks,
    decimal IdealFractionSum,
    bool IdealFractionIsBalanced,
    IReadOnlyList<string> Warnings);

public sealed record SaveUnitRequest(
    string Identifier,
    Guid? BlockId = null,
    /// <summary>Cria o bloco na hora quando não existir, evitando um cadastro em dois passos.</summary>
    string? NewBlockName = null,
    int? Floor = null,
    UnitKind Kind = UnitKind.Apartment,
    decimal? AreaM2 = null,
    decimal? IdealFraction = null,
    bool IsActive = true);

/// <summary>Resultado do recálculo das frações a partir da área privativa.</summary>
public sealed record RedistributeResult(
    int UnitsAffected,
    decimal IdealFractionSum,
    IReadOnlyList<UnitDto> Units);
