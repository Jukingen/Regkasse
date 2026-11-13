using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.DataExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin cross-tenant data-management dashboard (license lifecycle, RKSV retention, deletion requests).
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/data-management")]
[Produces("application/json")]
public sealed class AdminDataManagementController : ControllerBase
{
    private readonly ITenantDataManagementOverviewService _overview;

    public AdminDataManagementController(ITenantDataManagementOverviewService overview)
    {
        _overview = overview;
    }

    /// <summary>List all active tenants with data-management / RKSV retention status.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TenantDataManagementOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDataManagementOverviewDto>> List(CancellationToken ct = default)
    {
        var dto = await _overview.ListAsync(ct).ConfigureAwait(false);
        return Ok(dto);
    }
}
