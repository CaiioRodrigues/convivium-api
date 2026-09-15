namespace Convivium.Application.Metering;

using Convivium.Domain.Metering;

/// <summary>A linha de uma unidade na folha de leitura.</summary>
public sealed record MeterReadingLine(
    Guid UnitId,
    string UnitIdentifier,
    decimal PreviousReading,
    decimal? CurrentReading,
    decimal Consumption,
    decimal Amount,
    /// <summary>
    /// Verdadeiro quando a leitura anterior veio da competencia passada em vez
    /// de ter sido digitada. Serve para a tela nao deixar alguem sobrescrever
    /// sem perceber o fechamento do mes anterior.
    /// </summary>
    bool PreviousFromLastCompetence);

/// <summary>A folha de leitura de uma competencia inteira.</summary>
public sealed record MeterReadingSheet(
    /// <summary>Competencia como "MM/AAAA", igual aos demais contratos.</summary>
    string Competence,
    MeteredUtility Utility,
    decimal UnitPrice,
    DateOnly? ReadOn,
    IReadOnlyList<MeterReadingLine> Lines,
    decimal TotalConsumption,
    decimal TotalAmount,
    /// <summary>Quantas unidades ainda estao sem leitura atual.</summary>
    int PendingCount,
    /// <summary>
    /// Falso depois que o ciclo daquela competencia fecha: os valores ja foram
    /// para os boletos e mudar a leitura nao mudaria mais o que foi cobrado.
    /// </summary>
    bool IsEditable);

public sealed record SaveMeterReadingsRequest(
    string Competence,
    MeteredUtility Utility,
    decimal UnitPrice,
    DateOnly? ReadOn,
    IReadOnlyList<UnitReadingInput> Readings);

public sealed record UnitReadingInput(
    Guid UnitId,
    decimal? PreviousReading,
    decimal? CurrentReading);

/// <summary>
/// Converte o preco do botijao em preco por metro cubico.
/// </summary>
/// <remarks>
/// E como a conta e feita na pratica: compra-se o cilindro de 45 kg, que rende
/// cerca de 20 m3, e o preco do metro sai da divisao. Fica aqui para a tela
/// nao ter que fazer a conta — e para o numero que foi usado ficar explicito.
/// </remarks>
public sealed record CylinderPrice(decimal CylinderCost, decimal CubicMeters)
{
    public decimal PerCubicMeter => CubicMeters > 0
        ? Math.Round(CylinderCost / CubicMeters, 4, MidpointRounding.AwayFromZero)
        : 0m;
}
