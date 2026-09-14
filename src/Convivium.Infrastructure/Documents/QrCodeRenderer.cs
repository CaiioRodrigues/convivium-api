namespace Convivium.Infrastructure.Documents;

using QRCoder;

/// <summary>Gera o QR Code do PIX como PNG.</summary>
/// <remarks>
/// Usa <c>PngByteQRCode</c>, que produz o PNG em codigo gerenciado puro.
/// As variantes baseadas em System.Drawing exigiriam libgdiplus no servidor.
/// </remarks>
internal static class QrCodeRenderer
{
    public static byte[] Png(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();

        // Correcao de erro M: o padrao do BC para PIX, equilibra tamanho do
        // QR e tolerancia a leitura em papel amassado ou tela suja.
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);

        return qr.GetGraphic(pixelsPerModule);
    }
}
