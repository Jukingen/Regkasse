using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin manual restore with second-admin approval. Validation-only isolated database restore; never production.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/restore")]
[Produces("application/json")]
public sealed class AdminRestoreController : ControllerBase
{
    private readonly IManualRestoreTriggerService _service;
    private readonly IRestoreReportService _restoreReportService;
    private readonly IComplianceCheckService _complianceCheck;

    public AdminRestoreController(
        IManualRestoreTriggerService service,
        IRestoreReportService restoreReportService,
        IComplianceCheckService complianceCheck)
    {
        _service = service;
        _restoreReportService = restoreReportService;
        _complianceCheck = complianceCheck;
    }

    /// <summary>Request a validation-only restore (pending second Super Admin approval).</summary>
    [HttpPost("request")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RestoreRequestStatus>> CreateRequest(
        [FromBody] RestoreRequest body,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            var status = await _service.CreateRequestAsync(
                body,
                User.GetActorUserId() ?? "unknown",
                User.GetActorEmail(),
                correlationId,
                cancellationToken);
            return CreatedAtAction(nameof(GetRequest), new { requestId = status.RequestId }, status);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "MANUAL_RESTORE_DISABLED", error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "MANUAL_RESTORE_VALIDATION", error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "BACKUP_RUN_NOT_FOUND", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "MANUAL_RESTORE_CONFLICT", error = ex.Message });
        }
    }

    /// <summary>Approve or reject a pending request (different Super Admin; token from email).</summary>
    [HttpPost("approve/{requestId:guid}")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestoreRequestStatus>> ProcessApproval(
        Guid requestId,
        [FromBody] RestoreApprovalRequest body,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            var status = await _service.ProcessApprovalAsync(
                requestId,
                body,
                User.GetActorUserId() ?? "unknown",
                User.GetActorEmail(),
                correlationId,
                cancellationToken);
            return Ok(status);
        }
        catch (ManualRestoreApprovalException ex)
        {
            return BadRequest(new { code = ex.Code, error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "MANUAL_RESTORE_VALIDATION", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "MANUAL_RESTORE_APPROVAL_CONFLICT", error = ex.Message });
        }
    }

    /// <summary>Get restore request status.</summary>
    [HttpGet("request/{requestId:guid}")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestoreRequestStatus>> GetRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var status = await _service.GetStatusAsync(requestId, cancellationToken);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// RKSV-oriented compliance report for a manual restore request (validation-only workflow evidence).
    /// </summary>
    [HttpGet("request/{requestId:guid}/report")]
    [ProducesResponseType(typeof(RestoreReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestoreReportResponseDto>> GetReport(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var report = await _restoreReportService.GenerateRestoreReportAsync(requestId, cancellationToken);
        if (report is null)
            return NotFound();

        return Ok(new RestoreReportResponseDto
        {
            RestoreId = report.RestoreId,
            TenantId = report.TenantId,
            TenantName = report.TenantName,
            RestoredAt = report.RestoredAt,
            RestoredBy = report.RestoredBy,
            BackupId = report.BackupId,
            BackupDate = report.BackupDate,
            TablesRestored = report.TablesRestored,
            RecordsRestored = report.RecordsRestored,
            Status = report.Status,
            ComplianceChecked = report.ComplianceChecked,
            RksvCompliant = report.RksvCompliant,
            RksvComplianceNotes = report.RksvComplianceNotes,
            ComplianceFindings = report.ComplianceFindings.ToList(),
            ValidationOnly = report.ValidationOnly,
            TargetDatabaseName = report.TargetDatabaseName,
            RestoreVerificationRunId = report.RestoreVerificationRunId,
            DrillStatus = report.DrillStatus,
            FiscalSqlPassed = report.FiscalSqlPassed,
            PostRestoreContinuityChecksPassed = report.PostRestoreContinuityChecksPassed,
            CorrelationId = report.CorrelationId
        });
    }

    /// <summary>
    /// Pre-flight RKSV compliance check before requesting a validation restore (same-tenant, integrity, gates).
    /// Cross-tenant mismatch returns 404.
    /// </summary>
    [HttpGet("compliance-check")]
    [ProducesResponseType(typeof(RestoreComplianceCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestoreComplianceCheckResponseDto>> ComplianceCheck(
        [FromQuery] Guid backupRunId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var result = await _complianceCheck.CheckRestoreComplianceAsync(
            backupRunId,
            tenantId ?? Guid.Empty,
            cancellationToken);

        if (!result.Succeeded
            && string.Equals(result.Code, RestoreService.CrossTenantCode, StringComparison.Ordinal))
        {
            return NotFound(new { code = "BACKUP_RUN_NOT_FOUND", error = "Backup run was not found." });
        }

        if (!result.Succeeded
            && string.Equals(result.Code, ComplianceCheckService.BackupNotFoundCode, StringComparison.Ordinal))
        {
            return NotFound(new { code = result.Code, error = result.Error });
        }

        return Ok(new RestoreComplianceCheckResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            BackupRunId = result.BackupRunId,
            TenantId = result.TenantId,
            Checks = result.Checks.Select(c => new RestoreComplianceCheckItemDto
            {
                Name = c.Name,
                Passed = c.Passed,
                Detail = c.Detail
            }).ToList()
        });
    }

    /// <summary>Paginated restore request history (newest first).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(RestoreRequestHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RestoreRequestHistoryResponse>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await _service.GetHistoryAsync(page, pageSize, cancellationToken);
        return Ok(history);
    }
}
