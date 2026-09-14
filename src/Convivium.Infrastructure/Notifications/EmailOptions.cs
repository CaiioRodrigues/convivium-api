namespace Convivium.Infrastructure.Notifications;

/// <summary>Configuracao de envio de e-mail, lida da secao "Email".</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "localhost";

    /// <summary>1025 e a porta do Mailpit no docker-compose; 587 em producao.</summary>
    public int Port { get; set; } = 1025;

    public bool UseSsl { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "nao-responda@convivium.local";

    public string FromName { get; set; } = "Convivium";

    /// <summary>Tentativas antes de marcar a mensagem como falha definitiva.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Intervalo entre varreduras da fila.</summary>
    public int PollSeconds { get; set; } = 30;

    /// <summary>Mensagens processadas por ciclo.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Desliga o despachante, util em testes e em ambientes de leitura.</summary>
    public bool DispatcherEnabled { get; set; } = true;
}
