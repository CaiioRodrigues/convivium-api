namespace Convivium.Domain.Condominiums;

using System.Text.RegularExpressions;
using Convivium.Domain.Common;

/// <summary>
/// Valida e normaliza a chave PIX conforme o tipo declarado.
/// </summary>
/// <remarks>
/// Cada tipo tem um formato que o Banco Central exige. Uma chave invalida
/// gera um QR Code que o aplicativo do banco recusa, e o sindico so descobre
/// quando os moradores comecam a reclamar que nao conseguem pagar.
/// </remarks>
public static partial class PixKey
{
    /// <summary>
    /// Normaliza a chave para o formato que entra no BR Code, ou lanca com
    /// uma mensagem que diz exatamente o que esta errado.
    /// </summary>
    public static string Normalize(string? key, PixKeyType type)
    {
        string raw = (key ?? string.Empty).Trim();
        DomainException.ThrowIf(raw.Length == 0, "Informe a chave PIX.");

        return type switch
        {
            PixKeyType.Cpf => NormalizeDocument(raw, 11, BrazilianDocument.IsValidCpf, "CPF"),
            PixKeyType.Cnpj => NormalizeDocument(raw, 14, BrazilianDocument.IsValidCnpj, "CNPJ"),
            PixKeyType.Email => NormalizeEmail(raw),
            PixKeyType.Phone => NormalizePhone(raw),
            PixKeyType.Random => NormalizeRandom(raw),
            _ => throw new DomainException($"Tipo de chave PIX não suportado: {type}."),
        };
    }

    private static string NormalizeDocument(
        string raw,
        int length,
        Func<string, bool> isValid,
        string label)
    {
        string digits = BrazilianDocument.OnlyDigits(raw) ?? string.Empty;

        DomainException.ThrowIf(
            digits.Length != length,
            $"A chave do tipo {label} precisa ter {length} dígitos.");

        DomainException.ThrowIf(!isValid(digits), $"{label} inválido: confira os dígitos.");

        return digits;
    }

    private static string NormalizeEmail(string raw)
    {
        string email = raw.ToLowerInvariant();

        DomainException.ThrowIf(
            !EmailPattern().IsMatch(email),
            "Chave PIX de e-mail inválida.");

        DomainException.ThrowIf(
            email.Length > 77,
            "A chave PIX de e-mail não pode passar de 77 caracteres.");

        return email;
    }

    /// <summary>Telefone vai no padrao E.164, com o codigo do pais: +5531999998888.</summary>
    private static string NormalizePhone(string raw)
    {
        string digits = BrazilianDocument.OnlyDigits(raw) ?? string.Empty;

        // Numero brasileiro digitado sem o codigo do pais: acrescenta o 55.
        if (digits.Length is 10 or 11)
        {
            digits = $"55{digits}";
        }

        DomainException.ThrowIf(
            digits.Length is < 12 or > 13,
            "Chave PIX de telefone inválida. Use DDD e número, por exemplo 31999998888.");

        return $"+{digits}";
    }

    private static string NormalizeRandom(string raw)
    {
        DomainException.ThrowIf(
            !Guid.TryParse(raw, out Guid parsed),
            "A chave aleatória precisa estar no formato de um identificador, com 36 caracteres.");

        return parsed.ToString("D").ToLowerInvariant();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
