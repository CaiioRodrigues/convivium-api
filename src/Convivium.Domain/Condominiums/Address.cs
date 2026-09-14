namespace Convivium.Domain.Condominiums;

/// <summary>
/// Endereco no padrao brasileiro. Mapeado pelo EF Core como "owned type":
/// vira colunas na propria tabela do dono, sem virar tabela separada.
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string? Complement { get; set; }

    public string District { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    /// <summary>Sigla da UF, ex.: "MG".</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>CEP somente com digitos, ex.: "30140071".</summary>
    public string ZipCode { get; set; } = string.Empty;

    public override string ToString()
    {
        string complement = string.IsNullOrWhiteSpace(Complement) ? "" : $", {Complement}";
        return $"{Street}, {Number}{complement} - {District}, {City}/{State} - CEP {ZipCode}";
    }
}
