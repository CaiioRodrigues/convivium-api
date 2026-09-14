namespace Convivium.Domain.Payments;

using System.Globalization;
using System.Text;

/// <summary>
/// Monta o "copia e cola" do PIX no formato BR Code, especificado pelo
/// Banco Central com base no padrao EMV QRCPS.
/// </summary>
/// <remarks>
/// O payload e uma sequencia de campos TLV (tag, tamanho, valor): cada campo e
/// um id de 2 digitos, o tamanho em 2 digitos e o conteudo. O ultimo campo e
/// sempre o CRC16 calculado sobre todo o resto do texto.
/// </remarks>
public static class BrCodeBuilder
{
    private const string PixGui = "br.gov.bcb.pix";
    private const string CurrencyBrl = "986";
    private const string CountryBr = "BR";
    private const string MerchantCategoryDefault = "0000";

    private const int MaxReceiverName = 25;
    private const int MaxReceiverCity = 15;
    private const int MaxTransactionId = 25;
    private const int MaxDescription = 72;

    public static string Build(PixCharge charge)
    {
        ArgumentNullException.ThrowIfNull(charge);
        ArgumentException.ThrowIfNullOrWhiteSpace(charge.Key);

        string merchantAccount =
            Field("00", PixGui) +
            Field("01", charge.Key.Trim());

        if (!string.IsNullOrWhiteSpace(charge.Description))
        {
            merchantAccount += Field("02", Sanitize(charge.Description, MaxDescription));
        }

        // "12" marca a cobranca como de uso unico; "11" seria um QR estatico reutilizavel.
        string initiationMethod = charge.Amount is > 0 ? "12" : "11";
        string transactionId = NormalizeTransactionId(charge.TransactionId);

        var payload = new StringBuilder()
            .Append(Field("00", "01"))
            .Append(Field("01", initiationMethod))
            .Append(Field("26", merchantAccount))
            .Append(Field("52", MerchantCategoryDefault))
            .Append(Field("53", CurrencyBrl));

        if (charge.Amount is > 0)
        {
            payload.Append(Field("54", charge.Amount.Value.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        payload
            .Append(Field("58", CountryBr))
            .Append(Field("59", Sanitize(charge.ReceiverName, MaxReceiverName)))
            .Append(Field("60", Sanitize(charge.ReceiverCity, MaxReceiverCity)))
            .Append(Field("62", Field("05", transactionId)));

        // O CRC entra no final, mas o calculo inclui o proprio cabecalho "6304".
        payload.Append("6304");
        payload.Append(Crc16(payload.ToString()));

        return payload.ToString();
    }

    /// <summary>Monta um campo TLV: id + tamanho com 2 digitos + conteudo.</summary>
    private static string Field(string id, string value) =>
        $"{id}{value.Length:00}{value}";

    /// <summary>
    /// O txid aceita apenas letras e numeros. "***" e o valor convencionado
    /// quando nao ha identificador — o banco gera um no recebimento.
    /// </summary>
    private static string NormalizeTransactionId(string? transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return "***";
        }

        string cleaned = new(transactionId.Where(char.IsAsciiLetterOrDigit).ToArray());

        return cleaned.Length == 0
            ? "***"
            : cleaned[..Math.Min(cleaned.Length, MaxTransactionId)];
    }

    /// <summary>
    /// Remove acentuacao e caracteres fora do ASCII imprimivel: o padrao usa
    /// um conjunto restrito, e um "C" com cedilha quebra a leitura em alguns apps.
    /// </summary>
    private static string Sanitize(string value, int maxLength)
    {
        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsAscii(c) && !char.IsControl(c) ? c : ' ');
        }

        string result = builder.ToString().Normalize(NormalizationForm.FormC);
        result = string.Join(' ', result.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return result.Length <= maxLength ? result : result[..maxLength].TrimEnd();
    }

    /// <summary>CRC-16/CCITT-FALSE: polinomio 0x1021, valor inicial 0xFFFF, sem reflexao.</summary>
    private static string Crc16(string payload)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (byte b in Encoding.UTF8.GetBytes(payload))
        {
            crc ^= (ushort)(b << 8);

            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ polynomial)
                    : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
