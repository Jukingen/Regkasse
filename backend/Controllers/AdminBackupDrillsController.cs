using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Read-only restore-drill PDF. Does not enqueue, restore, or change backup content.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/backup")]
public sealed class AdminBackupDrillsController : ControllerBase
{
    private readonly IRestoreVerificationRunQueryService _runs;
    private readonly RestoreDrillPdfRenderer _pdf;
    private readonly IAuditLogService _audit;
    private readonly ICurrentTenantAccessor _tenant;

    public AdminBackupDrillsController(
        IRestoreVerificationRunQueryService runs,
        RestoreDrillPdfRenderer pdf,
        IAuditLogService audit,
        ICurrentTenantAccessor tenant)
    {
        _runs = runs;
        _pdf = pdf;
        _audit = audit;
        _tenant = tenant;
    }

    /// <summary>Single-drill PDF for a Passed or Failed drill. Cancelled drills are HTTP 400.</summary>
    [HttpPost("drills/{id:guid}/pdf")]
    [HasPermission(AppPermissions.SystemCritical)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(id, access: null, cancellationToken);
        if (run == null || !VisibleToCaller(run))
            return NotFound();

        byte[] bytes;
        try
        {
            bytes = _pdf.Render(run, User.GetActorUserId() ?? "unknown", DateTime.UtcNow);
        }
        catch (RestoreDrillNotReportableException)
        {
            return BadRequest(new { code = RestoreDrillPdfRenderer.NotReportableCode });
        }

        var actor = User.GetActorUserId() ?? "unknown";
        await _audit.LogSystemOperationAsync(
            action: "RESTORE_DRILL_PDF_DOWNLOADED",
            entityType: "RestoreVerificationRun",
            userId: actor,
            userRole: User.IsInRole(Roles.SuperAdmin) ? Roles.SuperAdmin : "User",
            description: "Restore drill PDF downloaded.",
            actionType: AuditEventType.RestoreDrillPdfDownloaded,
            entityId: id,
            tenantId: _tenant.TenantId,
            newValues: new
            {
                tenantId = _tenant.TenantId,
                drillId = id,
                actorUserId = actor
            });

        return File(bytes, "application/pdf", $"restore-drill-{id}.pdf");
    }

    private bool VisibleToCaller(RestoreVerificationRun run)
    {
        var sourceTenant = run.SourceBackupRun?.TenantId;
        if (_tenant.TenantId is Guid caller)
            return sourceTenant == caller;

        return User.IsInRole(Roles.SuperAdmin);
    }
}
