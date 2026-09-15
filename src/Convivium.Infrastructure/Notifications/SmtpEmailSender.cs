namespace Convivium.Infrastructure.Notifications;

using Convivium.Application.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

/// <summary>Envia a mensagem por SMTP usando MailKit.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var message = new MimeMessage();
        // O endereco e sempre o mesmo, porque precisa ser de dominio
        // verificado no provedor. O nome varia por condominio.
        message.From.Add(new MailboxAddress(
            email.FromName ?? _options.FromName,
            _options.FromAddress));
        message.To.Add(new MailboxAddress(email.ToName ?? email.ToAddress, email.ToAddress));
        message.Subject = email.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody,
        };

        foreach (EmailAttachment attachment in email.Attachments)
        {
            body.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ContentType.Parse(attachment.ContentType));
        }

        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();

        // StartTlsWhenAvailable cobre tanto o Mailpit local, que nao tem TLS,
        // quanto um relay de producao que exige STARTTLS na porta 587.
        SecureSocketOptions security = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
