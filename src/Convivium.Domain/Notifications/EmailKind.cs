namespace Convivium.Domain.Notifications;

public enum EmailKind
{
    /// <summary>A cobranca do mes foi publicada e ja pode ser paga.</summary>
    ChargeIssued = 1,

    /// <summary>Lembrete alguns dias antes do vencimento.</summary>
    ChargeReminder = 2,

    /// <summary>Aviso de cobranca vencida, com multa e juros ja calculados.</summary>
    ChargeOverdue = 3,

    /// <summary>Confirmacao de pagamento recebido.</summary>
    PaymentReceipt = 4,

    /// <summary>Convite de primeiro acesso ao sistema.</summary>
    Welcome = 5,

    PasswordReset = 6,

    /// <summary>Comunicado geral do sindico.</summary>
    Announcement = 7,
}
