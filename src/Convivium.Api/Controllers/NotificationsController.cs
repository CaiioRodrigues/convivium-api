namespace Convivium.Api.Controllers;

using Convivium.Api.Auth;
using Convivium.Application.Common;
using Convivium.Application.Notifications;
using Convivium.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Avisos por e-mail enviados aos moradores.</summary>
[Route("api/notificacoes")]
[Authorize(Policy = ConviviumPolicies.Finance)]
public sealed class NotificationsController(EmailOutboxService outbox) : ApiControllerBase
{
    /// <summary>
    /// Enfileira o aviso de cobranca disponivel para todas as unidades do ciclo.
    /// </summary>
    /// <remarks>
    /// O retorno lista as unidades sem e-mail cadastrado, para o sindico saber
    /// quem precisa ser avisado de outra forma.
    /// </remarks>
    [HttpPost("ciclos/{billingCycleId:guid}/enviar")]
    [ProducesResponseType<EmailQueueResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmailQueueResult>> SendCycleNotices(
        Guid billingCycleId,
        [FromQuery] EmailKind kind = EmailKind.ChargeIssued,
        CancellationToken cancellationToken = default)
        => Accepted(await outbox.QueueCycleNoticesAsync(billingCycleId, kind, cancellationToken));

    /// <summary>Enfileira lembretes das cobrancas que vencem daqui a N dias.</summary>
    [HttpPost("lembretes")]
    [ProducesResponseType<EmailQueueResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<EmailQueueResult>> SendReminders(
        [FromQuery] int daysBefore = 3,
        CancellationToken cancellationToken = default)
        => Accepted(await outbox.QueueDueRemindersAsync(daysBefore, cancellationToken));

    /// <summary>Enfileira avisos das cobrancas vencidas e ainda em aberto.</summary>
    [HttpPost("inadimplentes")]
    [ProducesResponseType<EmailQueueResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<EmailQueueResult>> SendOverdueNotices(
        [FromQuery] int minimumDaysLate = 1,
        CancellationToken cancellationToken = default)
        => Accepted(await outbox.QueueOverdueNoticesAsync(minimumDaysLate, cancellationToken));

    /// <summary>Fila de saida, para acompanhar o que foi enviado e o que falhou.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<EmailMessageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmailMessageDto>>> List(
        [FromQuery] EmailStatus? status,
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
        => Ok(await outbox.ListAsync(status, page, cancellationToken));

    /// <summary>Recoloca na fila uma mensagem que esgotou as tentativas.</summary>
    [HttpPost("{id:guid}/reenviar")]
    [ProducesResponseType<EmailMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmailMessageDto>> Retry(Guid id, CancellationToken cancellationToken)
        => Ok(await outbox.RetryAsync(id, cancellationToken));
}
