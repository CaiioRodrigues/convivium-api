namespace Convivium.Domain.Notifications;

public enum EmailStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    /// <summary>Esgotou as tentativas. Exige acao manual.</summary>
    Failed = 4,
    Cancelled = 5,
}
