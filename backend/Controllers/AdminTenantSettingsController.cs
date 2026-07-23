using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.TenantSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin four-eyes workflow for sensitive tenant settings (currency, country, timezone, fiscal).
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/tenant-settings")]
[Produces("application/json")]
public sealed class AdminTenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _settingsService;

    public AdminTenantSettingsController(ITenantSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentTenantSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentTenantSettingsDto>> GetCurrent(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { code = "TENANT_ID_REQUIRED", message = "tenantId is required." });

        var current = await _settingsService
            .GetCurrentSettingsAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
            return NotFound();

        return Ok(current);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSettingsHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantSettingsHistoryDto>>> GetHistory(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { code = "TENANT_ID_REQUIRED", message = "tenantId is required." });

        TenantSettingStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TenantSettingStatuses.TryParse(status, out var s))
                return BadRequest(new { code = "INVALID_STATUS", message = "Invalid status filter." });
            parsedStatus = s;
        }

        var history = await _settingsService
            .GetChangeHistoryAsync(tenantId, fromDate, toDate, parsedStatus, cancellationToken)
            .ConfigureAwait(false);
        return Ok(history);
    }

    [HttpPost("request")]
    [ProducesResponseType(typeof(SettingsChangeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsChangeResultDto>> RequestChange(
        [FromBody] RequestTenantSettingsChangeDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!TenantSettingTypes.TryParse(request.SettingType, out var settingType))
        {
            return BadRequest(new
            {
                code = "INVALID_SETTING_TYPE",
                message = "settingType must be currency, country, timezone, or fiscal_settings.",
            });
        }

        var result = await _settingsService
            .RequestSettingsChangeAsync(
                request.TenantId,
                settingType,
                request.NewValue,
                request.Reason,
                userId,
                cancellationToken)
            .ConfigureAwait(false);

        return MapResult(result);
    }

    [HttpPost("{changeId:guid}/approve")]
    [ProducesResponseType(typeof(SettingsChangeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsChangeResultDto>> ApproveChange(
        Guid changeId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _settingsService
            .ApproveSettingsChangeAsync(changeId, userId, cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result);
    }

    [HttpPost("{changeId:guid}/reject")]
    [ProducesResponseType(typeof(SettingsChangeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsChangeResultDto>> RejectChange(
        Guid changeId,
        [FromBody] RejectTenantSettingsChangeDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _settingsService
            .RejectSettingsChangeAsync(changeId, userId, body.Reason, cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result);
    }

    [HttpPost("{changeId:guid}/revert")]
    [ProducesResponseType(typeof(SettingsChangeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsChangeResultDto>> RevertChange(
        Guid changeId,
        [FromBody] RevertTenantSettingsChangeDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _settingsService
            .RevertSettingsChangeAsync(changeId, userId, body.Reason, cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result);
    }

    private ActionResult<SettingsChangeResultDto> MapResult(SettingsChangeResult result)
    {
        var dto = new SettingsChangeResultDto
        {
            Succeeded = result.Succeeded,
            ChangeId = result.ChangeId == Guid.Empty ? null : result.ChangeId,
            Error = result.Error,
            ErrorCode = result.ErrorCode,
            Warning = result.Warning,
        };

        if (!result.Succeeded)
        {
            if (string.Equals(result.ErrorCode, TenantSettingsErrorCodes.NotFound, StringComparison.Ordinal)
                || string.Equals(result.ErrorCode, TenantSettingsErrorCodes.TenantNotFound, StringComparison.Ordinal)
                || string.Equals(result.ErrorCode, TenantSettingsErrorCodes.CompanySettingsMissing, StringComparison.Ordinal))
            {
                return NotFound(new { code = result.ErrorCode, message = result.Error });
            }

            return BadRequest(new { code = result.ErrorCode, message = result.Error });
        }

        return Ok(dto);
    }
}
