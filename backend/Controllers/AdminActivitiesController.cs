using KasseAPI_Final.Authorization;
using KasseAPI_Final.Logging;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin activity feed and in-app notifications.</summary>
[Authorize]
[ApiController]
[Route("api/admin/activities")]
public sealed class AdminActivitiesController : ControllerBase
{
    private readonly IActivityEventService _activity;
    private readonly INotificationConfigService _notificationConfig;
    private readonly IActivityStreamHub _streamHub;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminActivitiesController> _logger;

    public AdminActivitiesController(
        IActivityEventService activity,
        INotificationConfigService notificationConfig,
        IActivityStreamHub streamHub,
        ISettingsTenantResolver tenantResolver,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminActivitiesController> logger)
    {
        _activity = activity;
        _notificationConfig = notificationConfig;
        _streamHub = streamHub;
        _tenantResolver = tenantResolver;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HasPermission(AppPermissions.SettingsView)]
    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<ActivitiesListResponseDto>> List(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string? severity = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var result = await _activity
            .ListAsync(userId, tenantId, limit, offset, severity, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>SSE stream: <c>event: activity</c> with JSON payload; <c>event: ping</c> keep-alive.</summary>
    [HasPermission(AppPermissions.SettingsView)]
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        // Action CT is normally RequestAborted; link explicitly so disconnect always cancels the hub.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            HttpContext.RequestAborted);
        var ct = linkedCts.Token;

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Guid tenantId;
        try
        {
            tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(ct).ConfigureAwait(false);
            var config = await _notificationConfig.GetAsync(tenantId, ct).ConfigureAwait(false);
            if (!config.InAppEnabled)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                await Response.WriteAsJsonAsync(
                    new { message = "In-app notifications are disabled for this tenant." },
                    ct).ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var userEmail = User.GetActorEmail();
        var tenantSlug = _tenantAccessor.TenantSlug;
        _logger.LogInformation(
            "Activity SSE stream started for tenant: {TenantSlug} ({TenantId}) user: {UserEmail} ({UserId})",
            string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
            LogIdFormatting.ShortGuid(tenantId),
            string.IsNullOrWhiteSpace(userEmail) ? "unknown" : userEmail.Trim(),
            LogIdFormatting.ShortId(userId));

        var disconnected = false;
        try
        {
            await foreach (var message in _streamHub
                .SubscribeAsync(tenantId, ct)
                .WithCancellation(ct)
                .ConfigureAwait(false))
            {
                await ActivitySseFormatter.WriteAsync(Response, message, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected or request aborted — expected for SSE.
            disconnected = true;
            _logger.LogInformation(
                "Activity SSE client disconnected for tenant: {TenantSlug} ({TenantId}) user: {UserEmail} ({UserId})",
                string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
                LogIdFormatting.ShortGuid(tenantId),
                string.IsNullOrWhiteSpace(userEmail) ? "unknown" : userEmail.Trim(),
                LogIdFormatting.ShortId(userId));
        }
        catch (IOException ex)
        {
            // Broken pipe / connection reset while writing an event.
            disconnected = true;
            _logger.LogInformation(
                ex,
                "Activity SSE client disconnected (IO) for tenant: {TenantSlug} ({TenantId}) user: {UserEmail} ({UserId})",
                string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
                LogIdFormatting.ShortGuid(tenantId),
                string.IsNullOrWhiteSpace(userEmail) ? "unknown" : userEmail.Trim(),
                LogIdFormatting.ShortId(userId));
        }
        catch (ObjectDisposedException ex)
        {
            disconnected = true;
            _logger.LogInformation(
                ex,
                "Activity SSE client disconnected (disposed) for tenant: {TenantSlug} ({TenantId}) user: {UserEmail} ({UserId})",
                string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
                LogIdFormatting.ShortGuid(tenantId),
                string.IsNullOrWhiteSpace(userEmail) ? "unknown" : userEmail.Trim(),
                LogIdFormatting.ShortId(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Activity SSE stream failed for tenant: {TenantSlug} ({TenantId})",
                string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
                LogIdFormatting.ShortGuid(tenantId));
            if (!Response.HasStarted)
                Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        finally
        {
            _logger.LogInformation(
                "Activity SSE stream ended for tenant: {TenantSlug} ({TenantId}) user: {UserEmail} ({UserId}) (disconnected={Disconnected})",
                string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim(),
                LogIdFormatting.ShortGuid(tenantId),
                string.IsNullOrWhiteSpace(userEmail) ? "unknown" : userEmail.Trim(),
                LogIdFormatting.ShortId(userId),
                disconnected || ct.IsCancellationRequested);
        }
    }

    [HasPermission(AppPermissions.SettingsView)]
    [HttpGet("unread-count")]
    [Produces("application/json")]
    public async Task<ActionResult<ActivitiesUnreadCountDto>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var count = await _activity.GetUnreadCountAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(count);
    }

    [HasPermission(AppPermissions.SettingsView)]
    [HttpGet("notification-config")]
    [Produces("application/json")]
    public async Task<ActionResult<NotificationConfig>> GetNotificationConfig(CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var config = await _notificationConfig.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(config);
    }

    [HasPermission(AppPermissions.SettingsManage)]
    [HttpPut("notification-config")]
    [Produces("application/json")]
    public async Task<ActionResult<NotificationConfig>> SaveNotificationConfig(
        [FromBody] NotificationConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
            return BadRequest(new { message = "Configuration body is required." });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var saved = await _notificationConfig.SaveAsync(tenantId, config, cancellationToken).ConfigureAwait(false);
        return Ok(saved);
    }

    [HasPermission(AppPermissions.SettingsView)]
    [HttpPost("{id:guid}/read")]
    [Produces("application/json")]
    public async Task<ActionResult<ActivityDto>> MarkRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var dto = await _activity.MarkEventReadAsync(userId, tenantId, id, cancellationToken).ConfigureAwait(false);
        if (dto == null)
            return NotFound();

        return Ok(dto);
    }

    [HasPermission(AppPermissions.SettingsView)]
    [HttpPost("mark-all-read")]
    [Produces("application/json")]
    public async Task<ActionResult<object>> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var marked = await _activity.MarkAllReadAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { markedCount = marked });
    }

    [HasPermission(AppPermissions.SettingsManage)]
    [HttpDelete("{id:guid}")]
    [Produces("application/json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await _activity.DeleteAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return BadRequest(new
            {
                message =
                    "Activity could not be deleted. It may not exist, belong to another tenant, or is newer than the retention window.",
            });
        }

        return NoContent();
    }
}
