using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public partial class AdminProductsController
{
    /// <summary>Start background demo catalog import with real-time progress (SignalR). POST api/admin/products/demo/import/jobs</summary>
    [HttpPost("demo/import/jobs")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(DemoImportJobStartResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemoImportJobStartResponseDto>> StartDemoImportJob(
        [FromBody] DemoImportRequest request,
        [FromServices] IDemoProductImportJobManager jobManager,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId == null)
            return BadRequest(new { error = "No tenant context" });

        var started = await jobManager
            .StartCatalogImportAsync(tenantId.Value, request, User, cancellationToken)
            .ConfigureAwait(false);

        return Accepted(started);
    }

    /// <summary>Poll demo import job progress (fallback when SignalR is unavailable).</summary>
    [HttpGet("demo/import/jobs/{jobId}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [ProducesResponseType(typeof(DemoImportJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DemoImportJobStatusDto> GetDemoImportJobStatus(
        string jobId,
        [FromServices] IDemoProductImportJobManager jobManager)
    {
        if (!jobManager.TryAuthorizeSubscription(User, jobId))
            return NotFound();

        var status = jobManager.GetStatus(jobId);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>Cancel a running demo import job.</summary>
    [HttpDelete("demo/import/jobs/{jobId}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult CancelDemoImportJob(
        string jobId,
        [FromServices] IDemoProductImportJobManager jobManager)
    {
        if (!jobManager.TryAuthorizeSubscription(User, jobId))
            return NotFound();

        if (!jobManager.TryCancel(jobId))
        {
            var status = jobManager.GetStatus(jobId);
            if (status == null)
                return NotFound();
            return Conflict(new { message = "Import job cannot be cancelled in its current state." });
        }

        return NoContent();
    }
}
