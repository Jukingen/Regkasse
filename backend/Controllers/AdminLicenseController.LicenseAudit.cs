using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Super Admin unified license audit trail (billing_audit_log + LICENSE_* audit_logs).
    /// </summary>
    [HttpGet("audit")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(LicenseAuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseAuditLogListResponse>> GetLicenseAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromServices] ILicenseAuditQueryService auditQuery = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await auditQuery
            .ListAsync(
                new LicenseAuditLogQuery(page, pageSize, tenantId, action, fromUtc, toUtc),
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}
