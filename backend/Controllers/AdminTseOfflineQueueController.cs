using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// TSE offline intent queue monitoring (legacy <c>offline_transactions</c> NonFiscalPending backlog).
/// Not the offline_orders snapshot system.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/offline-queue")]
[Produces("application/json")]
public sealed class AdminTseOfflineQueueController : ControllerBase
{
    private readonly ITseOfflineQueueService _queue;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminTseOfflineQueueController(
        ITseOfflineQueueService queue,
        ISettingsTenantResolver tenantResolver)
    {
        _queue = queue;
        _tenantResolver = tenantResolver;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.PaymentView)]
    [ProducesResponseType(typeof(TseOfflineQueueStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseOfflineQueueStatusDto>> GetStatus(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = await ResolveTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tid is null)
            return BadRequest(new { error = "tenantId is required (or select a tenant context)." });

        var status = await _queue.GetQueueStatusAsync(tid.Value, cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpGet]
    [HasPermission(AppPermissions.PaymentView)]
    [ProducesResponseType(typeof(IReadOnlyList<TseOfflineQueuedTransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseOfflineQueuedTransactionDto>>> ListQueued(
        [FromQuery] Guid? tenantId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var tid = await ResolveTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tid is null)
            return BadRequest(new { error = "tenantId is required (or select a tenant context)." });

        var rows = await _queue
            .GetQueuedTransactionsAsync(tid.Value, limit, cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows);
    }

    /// <summary>
    /// Soft-clear NonFiscalPending only (marks Failed). Hard delete is forbidden.
    /// confirmToken must be <c>SOFT_CLEAR</c>. SuperAdmin / system.critical.
    /// </summary>
    [HttpPost("soft-clear")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseOfflineQueueClearResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseOfflineQueueClearResultDto>> SoftClear(
        [FromBody] TseOfflineQueueClearRequestDto? body,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        body ??= new TseOfflineQueueClearRequestDto();
        var tid = await ResolveTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tid is null)
            return BadRequest(new { error = "tenantId is required (or select a tenant context)." });

        var actorId = User.GetActorUserId() ?? "system";
        var result = await _queue
            .SoftClearQueueAsync(tid.Value, body.ConfirmToken, body.Reason, actorId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("alert")]
    [HasPermission(AppPermissions.PaymentView)]
    [ProducesResponseType(typeof(TseOfflineQueueAlertResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseOfflineQueueAlertResultDto>> SendAlert(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = await ResolveTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tid is null)
            return BadRequest(new { error = "tenantId is required (or select a tenant context)." });

        var result = await _queue.SendQueueAlertAsync(tid.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    private async Task<Guid?> ResolveTenantAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is { } explicitId && explicitId != Guid.Empty)
            return explicitId;

        return await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
    }
}
