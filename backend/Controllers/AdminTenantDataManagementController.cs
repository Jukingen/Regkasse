using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.DataAccess;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Services.DataRights;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Customer data rights / expired-license data management (GDPR View/Export/Delete + RKSV retention).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/data-management")]
[Produces("application/json")]
public sealed class AdminTenantDataManagementController : ControllerBase
{
    private readonly IDataExportService _export;
    private readonly IDataDeletionService _deletion;
    private readonly ITenantDataDeletionRequestService _deletionRequests;
    private readonly ICustomerDataRightsService _rights;
    private readonly IDataAccessService _dataAccess;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _audit;

    public AdminTenantDataManagementController(
        IDataExportService export,
        IDataDeletionService deletion,
        ITenantDataDeletionRequestService deletionRequests,
        ICustomerDataRightsService rights,
        IDataAccessService dataAccess,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService audit)
    {
        _export = export;
        _deletion = deletion;
        _deletionRequests = deletionRequests;
        _rights = rights;
        _dataAccess = dataAccess;
        _tenantAccessor = tenantAccessor;
        _audit = audit;
    }

    [HttpGet]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(TenantDataManagementSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDataManagementSummaryDto>> GetSummary(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var summary = await _export.GetSummaryAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(summary);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>GDPR request type catalog (View / Export / Delete).</summary>
    [HttpGet("request-types")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(IReadOnlyList<DataRightsRequestTypeCatalogItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<DataRightsRequestTypeCatalogItemDto>> GetRequestTypes(Guid tenantId)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        return Ok(_rights.GetRequestTypeCatalog());
    }

    [HttpGet("requests")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDataRightsRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TenantDataRightsRequestDto>>> ListRequests(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        var rows = await _rights.ListAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpPost("requests")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(DataAccessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DataAccessResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataAccessResult>> CreateRequest(
        Guid tenantId,
        [FromBody] CreateDataRightsRequestDto? body,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        if (body == null || !DataRequestTypeExtensions.TryParse(body.Type, out var type))
            return BadRequest(new { message = "type is required (view, export, or delete)." });

        var userIdRaw = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = Guid.TryParse(userIdRaw, out var userId);

        var result = await _dataAccess
            .ProcessRequestAsync(tenantId, type, userId, body.Reason, ct)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            if (string.Equals(result.ErrorCode, DataAccessErrorCodes.NotFound, StringComparison.Ordinal)
                || (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return NotFound(result);
            }

            return BadRequest(result);
        }

        if (result.IsPending)
            return Accepted(result);

        return Ok(result);
    }

    [HttpGet("requests/{requestId:guid}")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(TenantDataRightsRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDataRightsRequestDto>> GetRequest(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        var row = await _rights.GetAsync(tenantId, requestId, ct).ConfigureAwait(false);
        if (row == null)
            return NotFound();
        return Ok(row);
    }

    [HttpGet("requests/{requestId:guid}/download")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadExportRequest(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var file = await _rights.DownloadExportAsync(tenantId, requestId, ct).ConfigureAwait(false);
            return File(file.Data, "application/zip", file.FileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("requests/{requestId:guid}/confirm")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(TenantDataRightsRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDataRightsRequestDto>> ConfirmRightsDelete(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var userId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var row = await _rights
                .ConfirmDeleteAsync(tenantId, requestId, userId, ct)
                .ConfigureAwait(false);
            return Ok(row);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("requests/{requestId:guid}/execute")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(DeletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeletionResult>> ExecuteRightsDelete(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        var userId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _rights
            .ExecuteDeleteAsync(tenantId, requestId, userId, ct)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            if (result.ErrorCode == DataDeletionErrorCodes.NotFound)
                return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>Legacy sync ZIP export (also records a completed Export rights request when possible).</summary>
    [HttpGet("export")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportZip(Guid tenantId, CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var userId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            // Prefer typed Export request (auto, &lt;24h) so artifacts and audit stay consistent.
            var rights = await _rights
                .CreateAsync(tenantId, TenantDataRightsRequestTypes.Export, reason: null, userId, ct)
                .ConfigureAwait(false);

            if (rights.CanDownload)
            {
                var file = await _rights.DownloadExportAsync(tenantId, rights.Id, ct).ConfigureAwait(false);
                await _deletionRequests.MarkExportCompletedAsync(tenantId, ct).ConfigureAwait(false);
                return File(file.Data, "application/zip", file.FileName);
            }

            // Fallback: immediate sync export if artifact not ready yet.
            var result = await _export.ExportAllDataAsync(tenantId, ct).ConfigureAwait(false);
            await _deletionRequests.MarkExportCompletedAsync(tenantId, ct).ConfigureAwait(false);

            var role = User.GetActorRole() ?? "Unknown";
            await _audit.LogSystemOperationAsync(
                action: "TENANT_DATA_EXPORT",
                entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
                userId: userId,
                userRole: role,
                description: $"Tenant data export ZIP ({result.FileName})",
                status: AuditLogStatus.Success,
                tenantId: tenantId).ConfigureAwait(false);

            return File(result.Data, "application/zip", result.FileName);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("deletion-request")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(TenantDataDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDataDeletionRequestDto>> RequestDeletion(
        Guid tenantId,
        [FromBody] RequestTenantDataDeletionDto? body,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var userIdRaw = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            _ = Guid.TryParse(userIdRaw, out var userId);
            var access = await _dataAccess
                .ProcessRequestAsync(tenantId, DataRequestType.Delete, userId, body?.Reason, ct)
                .ConfigureAwait(false);

            if (!access.Succeeded)
                return BadRequest(new { message = access.Error, code = access.ErrorCode ?? DataDeletionErrorCodes.NotArchived });

            if (access.Rights?.LinkedDeletionRequest != null)
                return Ok(access.Rights.LinkedDeletionRequest);

            // Should not happen; fall back for safety.
            var row = await _deletion
                .RequestDeletionAsync(tenantId, userIdRaw, body?.Reason, ct)
                .ConfigureAwait(false);
            return Ok(row);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, code = DataDeletionErrorCodes.NotArchived });
        }
    }

    [HttpPost("deletion-request/{requestId:guid}/confirm")]
    [HasPermission(AppPermissions.BackupManage)]
    [ProducesResponseType(typeof(TenantDataDeletionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDataDeletionRequestDto>> ConfirmDeletion(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (!TryAuthorizeTenant(tenantId, out var error))
            return error!;

        try
        {
            var userId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var row = await _deletion
                .ConfirmDeletionAsync(tenantId, requestId, userId, ct)
                .ConfigureAwait(false);
            return Ok(row);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Super Admin manual execute after the 7-day wait (same gates as auto-purge).</summary>
    [HttpPost("deletion-request/{requestId:guid}/execute")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(DeletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeletionResult>> ExecutePurge(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        _ = tenantId;

        var userId = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _deletion
            .ExecutePurgeAsync(requestId, userId, TenantDataDeletionExecutedVia.Manual, ct)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            if (result.ErrorCode == DataDeletionErrorCodes.NotFound)
                return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }

    private bool TryAuthorizeTenant(Guid tenantId, out ActionResult? error)
    {
        error = null;
        if (User.IsInRole(Roles.SuperAdmin))
            return true;

        if (_tenantAccessor.TenantId is Guid ambient && ambient == tenantId)
            return true;

        error = NotFound();
        return false;
    }
}
