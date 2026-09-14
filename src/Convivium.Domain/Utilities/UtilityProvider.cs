namespace Convivium.Domain.Utilities;

/// <summary>
/// Concessionaria de onde a fatura veio. Cada valor tem um leitor de PDF proprio,
/// porque cada empresa tem um layout diferente.
/// </summary>
public enum UtilityProvider
{
    /// <summary>Energia eletrica em Minas Gerais.</summary>
    Cemig = 1,

    /// <summary>Agua e esgoto em Minas Gerais.</summary>
    Copasa = 2,

    /// <summary>Gas canalizado em Minas Gerais.</summary>
    Gasmig = 3,

    /// <summary>Layout desconhecido: extrai o texto e pede revisao manual.</summary>
    Unknown = 99,
}
