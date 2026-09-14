namespace Convivium.Domain.Notifications;

using Convivium.Domain.Common;

/// <summary>
/// Mensagem na fila de saida (padrao outbox). A API grava a mensagem na mesma
/// transacao da operacao que a originou e devolve resposta na hora; um servico
/// em segundo plano faz o envio e as retentativas.
/// </summary>
/// <remarks>
/// Isso resolve dois problemas de uma vez: o SMTP fora do ar nao derruba a
/// requisicao, e nenhuma cobranca e publicada sem que o e-mail correspondente
/// exista para ser enviado.
/// </remarks>
public class EmailMessage : Entity
{
    /// <summary>Nulo em mensagens da plataforma, como recuperacao de senha.</summary>
    public Guid? CondominiumId { get; set; }

    public EmailKind Kind { get; set; }

    public string ToAddress { get; set; } = string.Empty;

    public string? ToName { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public string? TextBody { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Quando a mensagem fica elegivel para envio. Permite agendar lembretes.</summary>
    public DateTimeOffset ScheduledFor { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Cobranca relacionada. Quando presente, o boleto em PDF e gerado na hora
    /// do envio e vai anexado — assim o anexo nunca fica desatualizado.
    /// </summary>
    public Guid? ChargeId { get; set; }

    public bool AttachChargePdf { get; set; }
}
