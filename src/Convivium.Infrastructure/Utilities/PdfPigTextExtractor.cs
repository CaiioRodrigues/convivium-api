namespace Convivium.Infrastructure.Utilities;

using System.Text;
using Convivium.Application.Utilities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

/// <summary>
/// Extrai o texto de um PDF com PdfPig, em codigo gerenciado puro.
/// </summary>
/// <remarks>
/// Usa o extrator por ordem de conteudo, que respeita a ordem visual de
/// leitura. O <c>page.Text</c> cru devolve os fragmentos na ordem em que
/// foram desenhados, o que embaralha layouts em colunas — justamente o
/// formato de uma conta de luz.
/// </remarks>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        using PdfDocument document = PdfDocument.Open(pdf);
        var builder = new StringBuilder();

        foreach (Page page in document.GetPages())
        {
            string text;

            try
            {
                text = ContentOrderTextExtractor.GetText(page);
            }
            catch (Exception)
            {
                // Alguns PDFs de concessionaria tem estruturas que a analise
                // de layout nao digere. O texto cru e pior, mas e melhor do
                // que devolver nada e perder a fatura inteira.
                text = page.Text;
            }

            builder.AppendLine(text);
        }

        return builder.ToString();
    }
}
