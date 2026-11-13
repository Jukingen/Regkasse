using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE integration webhooks (register / test / delivery log).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/webhooks")]
[Produces("application/json")]
public sealed class AdminTseWebhooksController : ControllerBase
{
    private readonly ITseWebhookService _webhooks;

    public AdminTseWebhooksController(ITseWebhookService webhooks)
    {
        _webhooks = webhooks;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseWebhookRegistrationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseWebhookRegistrationDto>>> List(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _webhooks.ListWebhooksAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseWebhookRegistrationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseWebhookRegistrationDto>> Register(
        [FromBody] RegisterTseWebhookRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new RegisterTseWebhookRequestDto();
        try
        {
            return Ok(await _webhooks
                .RegisterWebhookAsync(body, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{webhookId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid webhookId, CancellationToken cancellationToken)
    {
        try
        {
            await _webhooks.DeleteWebhookAsync(webhookId, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{webhookId:guid}/test")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseWebhookDeliveryResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseWebhookDeliveryResultDto>> Test(
        Guid webhookId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _webhooks.TestWebhookAsync(webhookId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{webhookId:guid}/trigger")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseWebhookDeliveryResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseWebhookDeliveryResultDto>> Trigger(
        Guid webhookId,
        [FromBody] TriggerTseWebhookRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new TriggerTseWebhookRequestDto();
        try
        {
            return Ok(await _webhooks
                .TriggerWebhookAsync(
                    webhookId,
                    new TseWebhookEventDto
                    {
                        EventId = Guid.NewGuid(),
                        EventType = body.EventType,
                        OccurredAt = DateTime.UtcNow,
                        Payload = body.Payload,
                    },
                    cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{webhookId:guid}/events")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseWebhookDeliveryLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseWebhookDeliveryLogDto>>> Events(
        Guid webhookId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _webhooks
                .GetWebhookEventsAsync(webhookId, take, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
