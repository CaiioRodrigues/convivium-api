namespace Convivium.Domain.Common;

/// <summary>
/// Valida CPF e CNPJ pelos digitos verificadores.
/// </summary>
/// <remarks>
/// Conferir o digito pega o erro de digitacao no cadastro, que e onde ele
/// custa barato. Depois, um CNPJ errado vira chave PIX invalida e o morador
/// descobre na hora de pagar.
/// </remarks>
public static class BrazilianDocument
{
    private static readonly int[] CpfFirstWeights = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CpfSecondWeights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CnpjFirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] CnpjSecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Remove mascara e devolve so os digitos. Nulo quando nao sobra nada.</summary>
    public static string? OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string digits = new(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public static bool IsValidCpf(string? value)
    {
        string? digits = OnlyDigits(value);

        if (digits is not { Length: 11 } || AllDigitsEqual(digits))
        {
            return false;
        }

        return CheckDigit(digits, CpfFirstWeights) == digits[9]
            && CheckDigit(digits, CpfSecondWeights) == digits[10];
    }

    public static bool IsValidCnpj(string? value)
    {
        string? digits = OnlyDigits(value);

        if (digits is not { Length: 14 } || AllDigitsEqual(digits))
        {
            return false;
        }

        return CheckDigit(digits, CnpjFirstWeights) == digits[12]
            && CheckDigit(digits, CnpjSecondWeights) == digits[13];
    }

    /// <summary>Aceita CPF ou CNPJ, decidindo pelo tamanho.</summary>
    public static bool IsValidCpfOrCnpj(string? value) => OnlyDigits(value) switch
    {
        { Length: 11 } => IsValidCpf(value),
        { Length: 14 } => IsValidCnpj(value),
        _ => false,
    };

    /// <summary>
    /// Sequencias como 111.111.111-11 passam na conta do digito verificador,
    /// mas nao sao documentos reais — sao o resultado tipico de um campo
    /// preenchido de qualquer jeito.
    /// </summary>
    private static bool AllDigitsEqual(string digits) => digits.All(c => c == digits[0]);

    private static char CheckDigit(string digits, int[] weights)
    {
        int sum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        int remainder = sum % 11;
        int digit = remainder < 2 ? 0 : 11 - remainder;

        return (char)('0' + digit);
    }
}
