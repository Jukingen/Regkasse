using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
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

    public AdminActivitiesController(
        IActivityEventService activity,
        INotificationConfigService notificationConfig,
        IActivityStreamHub streamHub,
        ISettingsTenantResolver tenantResolver)
    {
        _activity = activity;
        _notificationConfig = notificationConfig;
        _streamHub = streamHub;
        _tenantResolver = tenantResolver;
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
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var config = await _notificationConfig.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!config.InAppEnabled)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(
                new { message = "In-app notifications are disabled for this tenant." },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var message in _streamHub.SubscribeAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            await ActivitySseFormatter.WriteAsync(Response, message, cancellationToken).ConfigureAwait(false);
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
