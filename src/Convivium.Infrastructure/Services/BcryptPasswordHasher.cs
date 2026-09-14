namespace Convivium.Infrastructure.Services;

using Convivium.Application.Abstractions;

/// <summary>
/// Hash de senha com BCrypt. O fator de trabalho define quanto custa calcular
/// um hash — e o mesmo custo que um ataque de forca bruta teria que pagar por tentativa.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>2^12 rodadas: cerca de 250 ms num servidor comum em 2026.</summary>
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrompido ou em formato antigo: trata como senha errada,
            // nunca como erro do servidor.
            return false;
        }
    }
}
