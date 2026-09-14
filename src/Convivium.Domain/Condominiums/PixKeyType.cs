namespace Convivium.Domain.Condominiums;

public enum PixKeyType
{
    Cpf = 1,
    Cnpj = 2,
    Email = 3,
    Phone = 4,
    /// <summary>Chave aleatoria (EVP), no formato de um GUID.</summary>
    Random = 5,
}
