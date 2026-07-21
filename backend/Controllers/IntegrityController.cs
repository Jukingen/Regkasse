using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Sprint 5: Internal consistency checks – sequence, orphan refunds, payment without invoice. Read-only.
/// Restore drills may call the same <see cref="IIntegrityCheckService"/> checks against the operational DB via <c>RestoreVerificationOrchestratorHostedService</c> (integrity validation step). See <c>AdminRestoreVerificationController</c> and docs/restore-verification-drill-runbook.md — live operational scope vs post-restore clone.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/integrity")]
[Produces("application/json")]
public class IntegrityController : ControllerBase
{
    private readonly IIntegrityCheckService _integrityService;
    private readonly ILogger<IntegrityController> _logger;

    public IntegrityController(IIntegrityCheckService integrityService, ILogger<IntegrityController> logger)
    {
        _integrityService = integrityService;
        _logger = logger;
    }

    /// <summary>GET: Run integrity checks and return report (sequence issues, orphan refunds, payment without invoice).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<IntegrityReportDto>> GetReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] bool includeDetails = false)
    {
        try
        {
            var report = await _integrityService.GetReportAsync(fromDate, toDate, includeDetails);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Integrity report failed");
            return StatusCode(500, new { message = "Internal server error while running integrity checks.", code = "INTEGRITY_CHECK_ERROR" });
        }
    }
}
