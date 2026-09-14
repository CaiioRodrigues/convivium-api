namespace Convivium.Infrastructure.Documents;

using System.Globalization;
using Convivium.Application.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>
/// Desenha o boleto de cobranca em PDF.
/// </summary>
/// <remarks>
/// O documento e montado a partir de um <see cref="ChargeDocument"/> ja
/// resolvido: nao consulta banco nem calcula encargos, so formata.
/// </remarks>
public sealed class ChargePdfRenderer : IChargeDocumentRenderer
{
    private static readonly CultureInfo Brazil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Lato vem embutida no QuestPDF, entao o PDF sai identico em qualquer
    /// servidor, sem depender de fonte instalada no sistema operacional.
    /// </summary>
    private const string BodyFont = "Lato";

    private const string Ink = "#1a1a1a";
    private const string Muted = "#6b7280";
    private const string Line = "#e5e7eb";
    private const string Accent = "#0f766e";
    private const string Danger = "#b91c1c";
    private const string Success = "#15803d";

    public byte[] Render(ChargeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.6f, Unit.Centimetre);
            page.DefaultTextStyle(text => text.FontSize(10).FontColor(Ink).FontFamily(BodyFont));

            page.Header().Element(header => ComposeHeader(header, document));
            page.Content().Element(content => ComposeContent(content, document));
            page.Footer().Element(footer => ComposeFooter(footer, document));
        })).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ChargeDocument document)
    {
        container.PaddingBottom(14).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text(document.CondominiumName)
                        .FontSize(16).SemiBold().FontColor(Accent);

                    if (!string.IsNullOrWhiteSpace(document.CondominiumCnpj))
                    {
                        info.Item().Text($"CNPJ {FormatCnpj(document.CondominiumCnpj)}")
                            .FontSize(8.5f).FontColor(Muted);
                    }

                    info.Item().Text(document.CondominiumAddress).FontSize(8.5f).FontColor(Muted);
                });

                row.ConstantItem(130).AlignRight().Column(badge =>
                {
                    badge.Item().AlignRight().Text("AVISO DE COBRANÇA")
                        .FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.08f);

                    (string label, string color) = document switch
                    {
                        { IsPaid: true } => ("PAGO", Success),
                        { IsOverdue: true } doc => ($"VENCIDO HÁ {doc.DaysLate} DIA(S)", Danger),
                        _ => ("EM ABERTO", Accent),
                    };

                    badge.Item().PaddingTop(4).AlignRight()
                        .Background(color).PaddingVertical(3).PaddingHorizontal(8)
                        .Text(label).FontSize(8).SemiBold().FontColor(Colors.White);
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Line);
        });
    }

    private static void ComposeContent(IContainer container, ChargeDocument document)
    {
        container.Column(column =>
        {
            column.Spacing(16);

            // Identificacao
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(cell => Field(cell, "Unidade", document.UnitIdentifier));
                row.RelativeItem().Element(cell =>
                    Field(cell, "Responsável", document.PayerName ?? "Não informado"));
                row.ConstantItem(90).Element(cell => Field(cell, "Competência", document.Competence));
                row.ConstantItem(90).Element(cell => Field(
                    cell,
                    "Vencimento",
                    document.DueDate.ToString("dd/MM/yyyy", Brazil),
                    document.IsOverdue ? Danger : Ink));
            });

            // Itens
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Descrição");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Valor (R$)");
                });

                foreach (ChargeItemDto item in document.Items)
                {
                    table.Cell().Element(BodyCell).Text(item.Description);
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(item.Amount));
                }
            });

            // Totais
            column.Item().AlignRight().Width(260).Column(totals =>
            {
                totals.Item().Element(cell => TotalRow(cell, "Subtotal", document.TotalAmount));

                if (document.PaidAmount > 0)
                {
                    totals.Item().Element(cell =>
                        TotalRow(cell, "Já recebido", -document.PaidAmount, Success));
                }

                if (document.LateFee > 0)
                {
                    totals.Item().Element(cell => TotalRow(cell, "Multa por atraso", document.LateFee, Danger));
                }

                if (document.Interest > 0)
                {
                    totals.Item().Element(cell =>
                        TotalRow(cell, $"Juros de mora ({document.DaysLate} dias)", document.Interest, Danger));
                }

                totals.Item().PaddingTop(6).BorderTop(1).BorderColor(Ink).PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text(document.IsPaid ? "TOTAL PAGO" : "TOTAL A PAGAR")
                        .FontSize(11).SemiBold();

                    row.ConstantItem(110).AlignRight()
                        .Text(Money(document.IsPaid ? document.TotalAmount : document.TotalDue))
                        .FontSize(13).Bold().FontColor(document.IsPaid ? Success : Accent);
                });
            });

            // PIX
            if (!document.IsPaid && !string.IsNullOrWhiteSpace(document.PixPayload))
            {
                column.Item().Element(cell => ComposePix(cell, document.PixPayload));
            }

            if (!string.IsNullOrWhiteSpace(document.Notes))
            {
                column.Item().Background("#f9fafb").Padding(10).Column(notes =>
                {
                    notes.Item().Text("Observações").FontSize(8).SemiBold().FontColor(Muted);
                    notes.Item().PaddingTop(2).Text(document.Notes).FontSize(9);
                });
            }

            if (document.IsOverdue)
            {
                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span(
                        "Encargos calculados até a data de emissão deste aviso, conforme o " +
                        "Código Civil, art. 1.336, § 1º, e a convenção do condomínio. " +
                        "O valor se altera a cada dia de atraso.");
                });
            }
        });
    }

    private static void ComposePix(IContainer container, string payload)
    {
        container.Border(1).BorderColor(Line).Padding(12).Row(row =>
        {
            row.ConstantItem(108).Image(QrCodeRenderer.Png(payload)).FitArea();

            row.RelativeItem().PaddingLeft(14).Column(column =>
            {
                column.Item().Text("Pague com PIX").FontSize(12).SemiBold().FontColor(Accent);

                column.Item().PaddingTop(2).Text(
                    "Abra o aplicativo do seu banco, escolha PIX, aponte para o QR Code " +
                    "ou use o código copia e cola abaixo.")
                    .FontSize(8.5f).FontColor(Muted);

                column.Item().PaddingTop(8).Text("Copia e cola")
                    .FontSize(7.5f).SemiBold().FontColor(Muted).LetterSpacing(0.06f);

                // Sem fonte monoespacada de proposito: QuestPDF nao usa fontes do
                // sistema por padrao, e exigir uma que talvez nao exista no servidor
                // faria a geracao do boleto falhar em producao. O codigo do PIX e
                // feito para copiar e colar, nao para ler caractere a caractere.
                column.Item().PaddingTop(3).Background("#f3f4f6").Padding(6)
                    .Text(payload).FontSize(6.8f).LineHeight(1.4f).LetterSpacing(0.02f);
            });
        });
    }

    private static void ComposeFooter(IContainer container, ChargeDocument document)
    {
        container.PaddingTop(10).BorderTop(1).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text($"Emitido em {DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm", Brazil)}")
                    .FontSize(7.5f).FontColor(Muted);

                if (!string.IsNullOrWhiteSpace(document.PublicUrl))
                {
                    column.Item().Text($"Consulte on-line: {document.PublicUrl}")
                        .FontSize(7.5f).FontColor(Muted);
                }
            });

            row.ConstantItem(130).AlignRight().AlignBottom()
                .Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(Muted));
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
        });
    }

    // --- Auxiliares de layout ---

    private static void Field(IContainer container, string label, string value, string color = Ink)
        => container.Column(column =>
        {
            column.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(Muted).LetterSpacing(0.06f);
            column.Item().PaddingTop(1).Text(value).FontSize(10.5f).SemiBold().FontColor(color);
        });

    private static void TotalRow(IContainer container, string label, decimal value, string color = Ink)
        => container.PaddingVertical(1.5f).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9).FontColor(Muted);
            row.ConstantItem(110).AlignRight().Text(Money(value)).FontSize(9.5f).FontColor(color);
        });

    private static IContainer HeaderCell(IContainer container) => container
        .BorderBottom(1).BorderColor(Ink).PaddingBottom(5)
        .DefaultTextStyle(text => text.FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.06f));

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1).BorderColor(Line).PaddingVertical(6);

    private static string Money(decimal value) => value.ToString("N2", Brazil);

    /// <summary>"12345678000195" -> "12.345.678/0001-95".</summary>
    private static string FormatCnpj(string digits) =>
        digits.Length == 14
            ? $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}"
            : digits;
}
