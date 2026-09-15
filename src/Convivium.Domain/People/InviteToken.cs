namespace Convivium.Domain.People;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Token de primeiro acesso: o valor sorteado e o hash que fica guardado.
/// </summary>
/// <remarks>
/// Fica num lugar so de proposito. O token e conferido comparando o hash do
/// que chega no link com o que esta no banco, entao gerar num canto e conferir
/// noutro, com duas implementacoes, e como convite para de validar meses
/// depois de alguem mexer numa das duas sem notar a outra.
/// </remarks>
public static class InviteToken
{
    /// <summary>
    /// Sorteia um token novo, em base64url para caber numa URL sem escapar.
    /// </summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// SHA-256 do token, em hexadecimal. E isto que o banco guarda — o valor
    /// original so existe no link que a pessoa recebe.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
