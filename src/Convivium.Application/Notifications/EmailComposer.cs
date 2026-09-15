namespace Convivium.Application.Notifications;

using System.Globalization;
using System.Net;
using System.Text;
using Convivium.Application.Billing;
using Convivium.Domain.Notifications;

/// <summary>
/// Monta o HTML dos e-mails transacionais.
/// </summary>
/// <remarks>
/// Layout em tabela com estilo inline de proposito: Gmail, Outlook e
/// aplicativos de celular descartam folhas de estilo e nao suportam flexbox
/// nem grid. O que funciona em navegador nao funciona em caixa de entrada.
/// </remarks>
public sealed class EmailComposer
{
    private static readonly CultureInfo Brazil = CultureInfo.GetCultureInfo("pt-BR");

    private const string Accent = "#0f766e";
    private const string Danger = "#b91c1c";
    private const string Ink = "#1f2937";
    private const string Muted = "#6b7280";
    private const string Border = "#e5e7eb";
    private const string Surface = "#f9fafb";

    public EmailContent ComposeCharge(EmailKind kind, ChargeDocument charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        (string subject, string headline, string intro) = kind switch
        {
            EmailKind.ChargeReminder => (
                $"Lembrete: cobrança de {charge.Competence} vence em {charge.DueDate:dd/MM}",
                "Sua cobrança vence em breve",
                $"A cobrança da unidade <strong>{Escape(charge.UnitIdentifier)}</strong> referente a " +
                $"{charge.Competence} vence em <strong>{charge.DueDate:dd/MM/yyyy}</strong>."),

            EmailKind.ChargeOverdue => (
                $"Cobrança de {charge.Competence} em atraso",
                "Cobrança vencida",
                $"A cobrança da unidade <strong>{Escape(charge.UnitIdentifier)}</strong> referente a " +
                $"{charge.Competence} venceu em <strong>{charge.DueDate:dd/MM/yyyy}</strong> e consta " +
                $"em aberto. O valor abaixo já inclui multa e juros."),

            EmailKind.PaymentReceipt => (
                $"Pagamento recebido - {charge.Competence}",
                "Pagamento confirmado",
                $"Recebemos o pagamento da cobrança de {charge.Competence} referente à unidade " +
                $"<strong>{Escape(charge.UnitIdentifier)}</strong>. Obrigado."),

            _ => (
                $"Cobrança de {charge.Competence} disponível",
                "Sua cobrança está disponível",
                $"A cobrança da unidade <strong>{Escape(charge.UnitIdentifier)}</strong> referente a " +
                $"{charge.Competence} já está disponível, com vencimento em " +
                $"<strong>{charge.DueDate:dd/MM/yyyy}</strong>."),
        };

        bool settled = kind == EmailKind.PaymentReceipt || charge.IsPaid;

        var body = new StringBuilder();
        body.Append(Paragraph(intro));
        body.Append(AmountPanel(charge, settled));
        body.Append(ItemsTable(charge));

        if (!settled && !string.IsNullOrWhiteSpace(charge.PixPayload))
        {
            body.Append(PixPanel(charge.PixPayload));
        }

        if (!string.IsNullOrWhiteSpace(charge.PublicUrl))
        {
            body.Append(Button(settled ? "Ver comprovante" : "Ver boleto completo", charge.PublicUrl));
        }

        if (!string.IsNullOrWhiteSpace(charge.Notes))
        {
            body.Append(NotePanel(charge.Notes));
        }

        return new EmailContent(
            subject,
            Layout(charge.CondominiumName, headline, body.ToString()),
            ComposeChargeText(headline, charge, settled));
    }

    public EmailContent ComposeWelcome(string condominiumName, string personName, string accessUrl)
    {
        var body = new StringBuilder();

        body.Append(Paragraph(
            $"Olá, {Escape(personName)}. Seu acesso ao portal do <strong>{Escape(condominiumName)}</strong> " +
            "foi criado. Por ele você acompanha suas cobranças, baixa boletos e consulta a " +
            "prestação de contas do condomínio."));

        body.Append(Button("Acessar o portal", accessUrl));

        body.Append(Paragraph(
            "Se você não reconhece este convite, ignore esta mensagem.",
            size: 13, color: Muted));

        return new EmailContent(
            $"Seu acesso ao {condominiumName}",
            Layout(condominiumName, "Bem-vindo ao portal", body.ToString()),
            $"Ola, {personName}. Seu acesso ao portal do {condominiumName} foi criado.\n\n{accessUrl}");
    }

    /// <summary>
    /// Link de redefinicao pedido na tela de login.
    /// </summary>
    /// <remarks>
    /// Diferente do convite em dois pontos que importam: diz de onde veio o
    /// pedido e o que fazer se nao foi a pessoa que pediu. Quem recebe um
    /// e-mail destes sem ter pedido precisa saber que ninguem entrou na conta
    /// dele — o link sozinho nao da acesso a nada ate ser usado.
    /// </remarks>
    public EmailContent ComposePasswordReset(
        string condominiumName,
        string personName,
        string resetUrl,
        TimeSpan validity)
    {
        string prazo = Validity(validity);

        var body = new StringBuilder();

        body.Append(Paragraph(
            $"Olá, {Escape(personName)}. Alguém pediu para redefinir a senha de acesso ao " +
            $"portal do <strong>{Escape(condominiumName)}</strong>. Se foi você, use o botão abaixo " +
            $"para escolher uma senha nova. O link vale por {prazo} e só pode ser usado uma vez."));

        body.Append(Button("Redefinir minha senha", resetUrl));

        body.Append(Paragraph(
            "Se você não pediu isso, ignore esta mensagem: sua senha atual continua valendo " +
            "e ninguém teve acesso à sua conta.",
            size: 13, color: Muted));

        return new EmailContent(
            $"Redefinir sua senha - {condominiumName}",
            Layout(condominiumName, "Redefinir senha", body.ToString()),
            $"Ola, {personName}. Alguem pediu para redefinir a senha do portal do " +
            $"{condominiumName}.\n\nSe foi voce, abra o link abaixo. Ele vale por {prazo} " +
            $"e e de uso unico.\n\n{resetUrl}\n\n" +
            "Se voce nao pediu isso, ignore esta mensagem: sua senha atual continua valendo.");
    }

    // --- Blocos ---

    private static string AmountPanel(ChargeDocument charge, bool settled)
    {
        decimal amount = settled ? charge.TotalAmount : charge.TotalDue;
        string label = settled ? "Valor pago" : "Total a pagar";
        string color = settled ? Accent : charge.IsOverdue ? Danger : Accent;

        var extra = new StringBuilder();

        if (!settled && charge.LateFee + charge.Interest > 0)
        {
            extra.Append(
                $"""
                 <div style="font-size:13px;color:{Muted};margin-top:6px;">
                   Inclui multa de R$ {Money(charge.LateFee)} e juros de R$ {Money(charge.Interest)}
                   por {charge.DaysLate} dia(s) de atraso.
                 </div>
                 """);
        }

        return $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                       style="background:{Surface};border:1px solid {Border};border-radius:8px;margin:20px 0;">
                  <tr>
                    <td style="padding:20px 24px;">
                      <div style="font-size:12px;color:{Muted};text-transform:uppercase;letter-spacing:0.6px;">
                        {label}
                      </div>
                      <div style="font-size:30px;font-weight:700;color:{color};margin-top:4px;">
                        R$ {Money(amount)}
                      </div>
                      {extra}
                    </td>
                  </tr>
                </table>
                """;
    }

    private static string ItemsTable(ChargeDocument charge)
    {
        if (charge.Items.Count == 0)
        {
            return string.Empty;
        }

        var rows = new StringBuilder();

        foreach (ChargeItemDto item in charge.Items)
        {
            rows.Append(
                $"""
                 <tr>
                   <td style="padding:9px 0;border-bottom:1px solid {Border};font-size:14px;color:{Ink};">
                     {Escape(item.Description)}
                   </td>
                   <td style="padding:9px 0;border-bottom:1px solid {Border};font-size:14px;color:{Ink};
                              text-align:right;white-space:nowrap;">
                     R$ {Money(item.Amount)}
                   </td>
                 </tr>
                 """);
        }

        return $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:8px 0 4px;">
                  <tr>
                    <td colspan="2" style="padding-bottom:6px;font-size:12px;color:{Muted};
                               text-transform:uppercase;letter-spacing:0.6px;border-bottom:2px solid {Ink};">
                      Composição
                    </td>
                  </tr>
                  {rows}
                </table>
                """;
    }

    private static string PixPanel(string payload) =>
        $"""
         <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                style="border:1px solid {Border};border-radius:8px;margin:20px 0;">
           <tr>
             <td style="padding:18px 20px;">
               <div style="font-size:15px;font-weight:700;color:{Accent};">Pague com PIX</div>
               <div style="font-size:13px;color:{Muted};margin-top:4px;">
                 Copie o código abaixo e cole no aplicativo do seu banco, na opção PIX
                 Copia e Cola. O QR Code está no boleto em anexo.
               </div>
               <div style="margin-top:12px;padding:10px;background:{Surface};border-radius:6px;
                           font-family:'Courier New',Courier,monospace;font-size:11px;
                           color:{Ink};word-break:break-all;line-height:1.6;">
                 {Escape(payload)}
               </div>
             </td>
           </tr>
         </table>
         """;

    private static string NotePanel(string notes) =>
        $"""
         <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                style="background:{Surface};border-radius:8px;margin:16px 0;">
           <tr>
             <td style="padding:14px 18px;font-size:13px;color:{Ink};">
               <strong style="color:{Muted};">Observações do síndico:</strong><br>{Escape(notes)}
             </td>
           </tr>
         </table>
         """;

    private static string Button(string label, string url) =>
        $"""
         <table role="presentation" cellpadding="0" cellspacing="0" style="margin:22px 0;">
           <tr>
             <td style="background:{Accent};border-radius:6px;">
               <a href="{Escape(url)}"
                  style="display:inline-block;padding:12px 26px;color:#ffffff;font-size:15px;
                         font-weight:600;text-decoration:none;">{Escape(label)}</a>
             </td>
           </tr>
         </table>
         """;

    private static string Paragraph(string html, int size = 15, string color = Ink) =>
        $"""<p style="margin:0 0 14px;font-size:{size}px;line-height:1.6;color:{color};">{html}</p>""";

    private static string Layout(string condominiumName, string headline, string body) =>
        $"""
         <!DOCTYPE html>
         <html lang="pt-BR">
         <head>
           <meta charset="utf-8">
           <meta name="viewport" content="width=device-width,initial-scale=1">
           <title>{Escape(headline)}</title>
         </head>
         <body style="margin:0;padding:0;background:#f3f4f6;">
           <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;">
             <tr>
               <td align="center" style="padding:24px 12px;">
                 <table role="presentation" width="600" cellpadding="0" cellspacing="0"
                        style="max-width:600px;width:100%;background:#ffffff;border-radius:10px;
                               overflow:hidden;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',
                               Roboto,Helvetica,Arial,sans-serif;">
                   <tr>
                     <td style="padding:22px 28px;border-bottom:3px solid {Accent};">
                       <div style="font-size:19px;font-weight:700;color:{Accent};">
                         {Escape(condominiumName)}
                       </div>
                     </td>
                   </tr>
                   <tr>
                     <td style="padding:26px 28px 8px;">
                       <h1 style="margin:0 0 14px;font-size:21px;font-weight:700;color:{Ink};">
                         {Escape(headline)}
                       </h1>
                       {body}
                     </td>
                   </tr>
                   <tr>
                     <td style="padding:16px 28px 24px;border-top:1px solid {Border};">
                       <div style="font-size:12px;color:{Muted};line-height:1.6;">
                         Mensagem automática do sistema de gestão do condomínio.
                         Não responda a este e-mail.
                       </div>
                     </td>
                   </tr>
                 </table>
               </td>
             </tr>
           </table>
         </body>
         </html>
         """;

    /// <summary>
    /// Versao em texto puro. Vai junto no mesmo envio: clientes que bloqueiam
    /// HTML mostram esta, e a presenca dela reduz a chance de cair em spam.
    /// </summary>
    private static string ComposeChargeText(string headline, ChargeDocument charge, bool settled)
    {
        var text = new StringBuilder();

        text.AppendLine(charge.CondominiumName);
        text.AppendLine(new string('-', charge.CondominiumName.Length));
        text.AppendLine();
        text.AppendLine(headline);
        text.AppendLine();
        text.AppendLine($"Unidade: {charge.UnitIdentifier}");
        text.AppendLine($"Competencia: {charge.Competence}");
        text.AppendLine($"Vencimento: {charge.DueDate:dd/MM/yyyy}");
        text.AppendLine();

        foreach (ChargeItemDto item in charge.Items)
        {
            text.AppendLine($"  {item.Description}: R$ {Money(item.Amount)}");
        }

        if (!settled && charge.LateFee + charge.Interest > 0)
        {
            text.AppendLine($"  Multa: R$ {Money(charge.LateFee)}");
            text.AppendLine($"  Juros ({charge.DaysLate} dias): R$ {Money(charge.Interest)}");
        }

        text.AppendLine();
        text.AppendLine($"{(settled ? "VALOR PAGO" : "TOTAL A PAGAR")}: R$ " +
                        $"{Money(settled ? charge.TotalAmount : charge.TotalDue)}");

        if (!settled && !string.IsNullOrWhiteSpace(charge.PixPayload))
        {
            text.AppendLine();
            text.AppendLine("PIX copia e cola:");
            text.AppendLine(charge.PixPayload);
        }

        if (!string.IsNullOrWhiteSpace(charge.PublicUrl))
        {
            text.AppendLine();
            text.AppendLine($"Boleto completo: {charge.PublicUrl}");
        }

        return text.ToString();
    }

    private static string Money(decimal value) => value.ToString("N2", Brazil);

    /// <summary>"1 hora", "2 horas", "7 dias" — o prazo escrito como se fala.</summary>
    private static string Validity(TimeSpan validity) => validity switch
    {
        { TotalDays: >= 2 } => $"{(int)validity.TotalDays} dias",
        { TotalDays: >= 1 } => "1 dia",
        { TotalHours: >= 2 } => $"{(int)validity.TotalHours} horas",
        { TotalHours: >= 1 } => "1 hora",
        _ => $"{(int)validity.TotalMinutes} minutos",
    };

    /// <summary>
    /// Escapa o texto antes de entrar no HTML. Nome de morador e observacao do
    /// sindico sao dados digitados por pessoas e nao podem virar marcacao.
    /// </summary>
    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
