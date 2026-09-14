namespace Convivium.Application.Notifications;

using Convivium.Application.Abstractions;
using Convivium.Application.Common;
using Convivium.Domain.Billing;
using Convivium.Domain.Common;
using Convivium.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Enfileira e-mails na tabela de saida.
/// </summary>
/// <remarks>
/// A mensagem e gravada na mesma transacao da operacao que a originou, e um
/// servico em segundo plano faz o envio. Assim o SMTP fora do ar nao derruba
/// a requisicao, e nenhuma cobranca e publicada sem que o e-mail
/// correspondente exista para ser enviado.
/// </remarks>
public sealed class EmailOutboxService(IApplicationDbContext db, IClock clock)
{
    /// <summary>Enfileira o aviso de cobranca para todas as cobrancas de um ciclo.</summary>
    public async Task<EmailQueueResult> QueueCycleNoticesAsync(
        Guid billingCycleId,
        EmailKind kind = EmailKind.ChargeIssued,
        CancellationToken cancellationToken = default)
    {
        var charges = await db.Charges
            .Include(c => c.Unit)
            .Include(c => c.Payer)
            .Where(c => c.BillingCycleId == billingCycleId)
            .Where(c => c.Status != ChargeStatus.Cancelled)
            .ToListAsync(cancellationToken);

        DomainException.ThrowIf(charges.Count == 0, "Este ciclo nao tem cobrancas para notificar.");

        return await QueueForChargesAsync(charges, kind, clock.Now, cancellationToken);
    }

    /// <summary>
    /// Enfileira lembretes das cobrancas que vencem daqui a N dias.
    /// </summary>
    public async Task<EmailQueueResult> QueueDueRemindersAsync(
        int daysBefore = 3,
        CancellationToken cancellationToken = default)
    {
        DomainException.ThrowIf(daysBefore is < 0 or > 30, "Informe de 0 a 30 dias de antecedencia.");

        DateOnly target = clock.Today.AddDays(daysBefore);

        var charges = await db.Charges
            .Include(c => c.Unit)
            .Include(c => c.Payer)
            .Where(c => c.DueDate == target)
            .Where(c => c.Status == ChargeStatus.Open || c.Status == ChargeStatus.PartiallyPaid)
            .ToListAsync(cancellationToken);

        return await QueueForChargesAsync(charges, EmailKind.ChargeReminder, clock.Now, cancellationToken);
    }

    /// <summary>Enfileira avisos das cobrancas vencidas e ainda em aberto.</summary>
    public async Task<EmailQueueResult> QueueOverdueNoticesAsync(
        int minimumDaysLate = 1,
        CancellationToken cancellationToken = default)
    {
        DateOnly limit = clock.Today.AddDays(-Math.Max(1, minimumDaysLate));

        var charges = await db.Charges
            .Include(c => c.Unit)
            .Include(c => c.Payer)
            .Where(c => c.DueDate <= limit)
            .Where(c => c.Status == ChargeStatus.Open
                     || c.Status == ChargeStatus.PartiallyPaid
                     || c.Status == ChargeStatus.Overdue)
            .ToListAsync(cancellationToken);

        return await QueueForChargesAsync(charges, EmailKind.ChargeOverdue, clock.Now, cancellationToken);
    }

    /// <summary>Enfileira o comprovante de um pagamento recebido.</summary>
    public async Task QueuePaymentReceiptAsync(
        Guid chargeId,
        CancellationToken cancellationToken = default)
    {
        Charge? charge = await db.Charges
            .Include(c => c.Unit)
            .Include(c => c.Payer)
            .FirstOrDefaultAsync(c => c.Id == chargeId, cancellationToken);

        if (charge is not null)
        {
            await QueueForChargesAsync([charge], EmailKind.PaymentReceipt, clock.Now, cancellationToken);
        }
    }

    private async Task<EmailQueueResult> QueueForChargesAsync(
        IReadOnlyCollection<Charge> charges,
        EmailKind kind,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken)
    {
        var skipped = new List<string>();
        int queued = 0;

        foreach (Charge charge in charges)
        {
            string? address = charge.Payer?.Email;

            // Sem e-mail nao ha o que enfileirar. Nao e erro: parte dos
            // proprietarios so tem telefone no cadastro. O chamador recebe a
            // lista para o sindico saber quem precisa ser avisado de outro jeito.
            if (string.IsNullOrWhiteSpace(address))
            {
                skipped.Add(charge.Unit?.FullIdentifier ?? charge.UnitId.ToString());
                continue;
            }

            bool alreadyQueued = await db.EmailMessages.AnyAsync(
                m => m.ChargeId == charge.Id
                  && m.Kind == kind
                  && (m.Status == EmailStatus.Pending || m.Status == EmailStatus.Sending),
                cancellationToken);

            // Evita que clicar duas vezes em "notificar" mande o mesmo aviso
            // duas vezes para o mesmo morador.
            if (alreadyQueued)
            {
                continue;
            }

            db.EmailMessages.Add(new EmailMessage
            {
                CondominiumId = charge.CondominiumId,
                Kind = kind,
                ToAddress = address.Trim(),
                ToName = charge.Payer?.Name,
                Subject = BuildPlaceholderSubject(kind, charge),
                HtmlBody = string.Empty,
                ScheduledFor = scheduledFor,
                ChargeId = charge.Id,
                AttachChargePdf = kind != EmailKind.PaymentReceipt,
            });

            charge.NotifiedAt = clock.Now;
            queued++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new EmailQueueResult(queued, skipped.Count, skipped);
    }

    /// <summary>
    /// Assunto provisorio, so para a mensagem aparecer legivel na fila. O
    /// assunto e o corpo definitivos sao montados na hora do envio, para que
    /// multa e juros de uma cobranca vencida reflitam o dia do envio e nao o
    /// dia em que a mensagem entrou na fila.
    /// </summary>
    private static string BuildPlaceholderSubject(EmailKind kind, Charge charge) => kind switch
    {
        EmailKind.ChargeReminder => $"Lembrete de vencimento - {charge.Competence}",
        EmailKind.ChargeOverdue => $"Cobranca em atraso - {charge.Competence}",
        EmailKind.PaymentReceipt => $"Pagamento recebido - {charge.Competence}",
        _ => $"Cobranca disponivel - {charge.Competence}",
    };

    public async Task<PagedResult<EmailMessageDto>> ListAsync(
        EmailStatus? status,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        IQueryable<EmailMessage> query = db.EmailMessages.AsNoTracking();

        if (status is { } value)
        {
            query = query.Where(m => m.Status == value);
        }

        int total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip(page.Skip)
            .Take(page.NormalizedPageSize)
            .Select(m => new EmailMessageDto(
                m.Id, m.Kind, m.ToAddress, m.ToName, m.Subject, m.Status,
                m.Attempts, m.LastError, m.ScheduledFor, m.SentAt, m.ChargeId))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailMessageDto>(items, total, page.NormalizedPage, page.NormalizedPageSize);
    }

    /// <summary>Recoloca na fila uma mensagem que esgotou as tentativas.</summary>
    public async Task<EmailMessageDto> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EmailMessage message = await db.EmailMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Mensagem nao encontrada.");

        DomainException.ThrowIf(
            message.Status == EmailStatus.Sent,
            "Esta mensagem ja foi enviada.");

        message.Status = EmailStatus.Pending;
        message.Attempts = 0;
        message.LastError = null;
        message.ScheduledFor = clock.Now;

        await db.SaveChangesAsync(cancellationToken);

        return new EmailMessageDto(
            message.Id, message.Kind, message.ToAddress, message.ToName, message.Subject,
            message.Status, message.Attempts, message.LastError,
            message.ScheduledFor, message.SentAt, message.ChargeId);
    }
}
