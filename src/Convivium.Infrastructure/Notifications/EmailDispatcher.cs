namespace Convivium.Infrastructure.Notifications;

using Convivium.Application.Abstractions;
using Convivium.Application.Billing;
using Convivium.Application.Notifications;
using Convivium.Domain.Notifications;
using Convivium.Infrastructure.Persistence;
using Convivium.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Varre a fila de saida e entrega as mensagens pendentes.
/// </summary>
/// <remarks>
/// Roda com um contexto de sistema, sem condominio ativo, porque a fila
/// atravessa todos os condominios. Assume uma unica instancia processando:
/// para rodar varias replicas seria preciso reservar as linhas com
/// "FOR UPDATE SKIP LOCKED" em vez da reserva otimista usada aqui.
/// </remarks>
public sealed class EmailDispatcher(
    IServiceProvider services,
    IOptions<EmailOptions> options,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
        {
            logger.LogInformation("Despachante de e-mail desabilitado por configuracao.");
            return;
        }

        await RecoverStuckMessagesAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.PollSeconds));
        using var timer = new PeriodicTimer(interval);

        logger.LogInformation(
            "Despachante de e-mail ativo, varrendo a fila a cada {Seconds}s.", interval.TotalSeconds);

        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um ciclo que falha nao pode derrubar o servico: a proxima
                // varredura tenta de novo.
                logger.LogError(ex, "Falha ao processar a fila de e-mail.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Devolve para a fila mensagens que ficaram em "Sending" — sinal de que
    /// o processo caiu no meio de um envio.
    /// </summary>
    private async Task RecoverStuckMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = services.CreateScope();
            await using ConviviumDbContext db = CreateSystemDbContext(scope);

            int recovered = await db.EmailMessages
                .Where(m => m.Status == EmailStatus.Sending)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(m => m.Status, EmailStatus.Pending),
                    cancellationToken);

            if (recovered > 0)
            {
                logger.LogWarning(
                    "{Count} mensagem(ns) presa(s) em envio foram devolvidas para a fila.", recovered);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nao foi possivel recuperar mensagens presas na fila.");
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        await using ConviviumDbContext db = CreateSystemDbContext(scope);

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        DateTimeOffset now = clock.Now;

        // Reserva o lote marcando como "Sending" antes de sair para a rede:
        // se o envio demorar, uma varredura concorrente nao pega as mesmas linhas.
        var batch = await db.EmailMessages
            .Where(m => m.Status == EmailStatus.Pending && m.ScheduledFor <= now)
            .OrderBy(m => m.ScheduledFor)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (EmailMessage message in batch)
        {
            message.Status = EmailStatus.Sending;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Nome do condominio de cada mensagem, para assinar o remetente. Uma
        // consulta para o lote inteiro, e nao uma por mensagem.
        var condominios = await db.Condominiums
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => batch.Select(m => m.CondominiumId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var composer = scope.ServiceProvider.GetRequiredService<EmailComposer>();
        var renderer = scope.ServiceProvider.GetRequiredService<IChargeDocumentRenderer>();
        var billing = ActivatorUtilities.CreateInstance<BillingService>(scope.ServiceProvider, db);

        int sent = 0, failed = 0;

        foreach (EmailMessage message in batch)
        {
            try
            {
                OutgoingEmail email = await BuildAsync(
                    message, billing, composer, renderer, cancellationToken);

                if (message.CondominiumId is { } condominioId
                    && condominios.TryGetValue(condominioId, out string? nomeDoCondominio))
                {
                    email = email with { FromName = nomeDoCondominio };
                }

                await sender.SendAsync(email, cancellationToken);

                // Guarda o que de fato saiu: a fila vira trilha de auditoria
                // do que cada morador recebeu.
                message.Subject = email.Subject;
                message.HtmlBody = email.HtmlBody;
                message.TextBody = email.TextBody;
                message.Status = EmailStatus.Sent;
                message.SentAt = clock.Now;
                message.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = Truncate(ex.Message, 2000);

                if (message.Attempts >= _options.MaxAttempts)
                {
                    message.Status = EmailStatus.Failed;
                    logger.LogError(
                        ex, "E-mail {Id} para {To} falhou definitivamente apos {Attempts} tentativas.",
                        message.Id, message.ToAddress, message.Attempts);
                }
                else
                {
                    // Recuo exponencial: 1, 2, 4, 8 minutos. Um servidor SMTP
                    // sobrecarregado piora se for martelado a cada 30 segundos.
                    var delay = TimeSpan.FromMinutes(Math.Pow(2, message.Attempts - 1));
                    message.Status = EmailStatus.Pending;
                    message.ScheduledFor = clock.Now.Add(delay);

                    logger.LogWarning(
                        "E-mail {Id} para {To} falhou (tentativa {Attempts}), nova tentativa em {Delay}.",
                        message.Id, message.ToAddress, message.Attempts, delay);
                }

                failed++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (sent > 0 || failed > 0)
        {
            logger.LogInformation("Fila de e-mail: {Sent} enviado(s), {Failed} com falha.", sent, failed);
        }
    }

    /// <summary>
    /// Monta a mensagem no momento do envio.
    /// </summary>
    /// <remarks>
    /// O corpo nao e montado no enfileiramento de proposito: multa e juros de
    /// uma cobranca vencida mudam todo dia, e um lembrete agendado para daqui
    /// a tres dias sairia com o valor de hoje. O anexo em PDF e gerado aqui
    /// pelo mesmo motivo.
    /// </remarks>
    private static async Task<OutgoingEmail> BuildAsync(
        EmailMessage message,
        BillingService billing,
        EmailComposer composer,
        IChargeDocumentRenderer renderer,
        CancellationToken cancellationToken)
    {
        if (message.ChargeId is not { } chargeId)
        {
            return new OutgoingEmail
            {
                ToAddress = message.ToAddress,
                ToName = message.ToName,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody,
            };
        }

        // Sem restricao de pessoa: quem monta o e-mail e a fila, que roda
        // com contexto de sistema e ja sabe para quem esta enviando.
        ChargeDocument document = await billing.BuildDocumentAsync(
            chargeId, onlyForPersonId: null, cancellationToken);
        EmailContent content = composer.ComposeCharge(message.Kind, document);

        var attachments = new List<EmailAttachment>();

        if (message.AttachChargePdf)
        {
            string fileName = $"boleto-{document.Competence.Replace('/', '-')}.pdf";
            attachments.Add(new EmailAttachment(fileName, "application/pdf", renderer.Render(document)));
        }

        return new OutgoingEmail
        {
            ToAddress = message.ToAddress,
            ToName = message.ToName,
            Subject = content.Subject,
            HtmlBody = content.HtmlBody,
            TextBody = content.TextBody,
            Attachments = attachments,
        };
    }

    /// <summary>
    /// Constroi um contexto sem condominio ativo. O contexto HTTP nao serve
    /// aqui: o servico roda fora de qualquer requisicao e a fila e global.
    /// </summary>
    private static ConviviumDbContext CreateSystemDbContext(IServiceScope scope)
    {
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ConviviumDbContext>>();
        return new ConviviumDbContext(options, new SystemTenantContext());
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
