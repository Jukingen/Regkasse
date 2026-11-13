using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin monitoring for both offline order snapshots and legacy TSE intent queues.</summary>
[Authorize]
[ApiController]
[Route("api/admin/offline-monitoring")]
[Produces("application/json")]
public class AdminOfflineMonitoringController : BaseController
{
    private readonly IOfflineMonitoringService _monitoring;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminOfflineMonitoringController(
        IOfflineMonitoringService monitoring,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminOfflineMonitoringController> logger) : base(logger)
    {
        _monitoring = monitoring;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<OfflineSystemStatus>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return Ok(await _monitoring.GetSystemStatusAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("orders/stats")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<OfflineOrderStats>> GetOrderStats(CancellationToken cancellationToken)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return Ok(await _monitoring.GetOrderStatsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("transactions/stats")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<OfflineTransactionStats>> GetTransactionStats(CancellationToken cancellationToken)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return Ok(await _monitoring.GetTransactionStatsAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("anomalies")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<IReadOnlyList<OfflineAnomaly>>> GetAnomalies(CancellationToken cancellationToken)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return Ok(await _monitoring.CheckAnomaliesAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sync-health")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<SyncHealth>> GetSyncHealth(CancellationToken cancellationToken)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return Ok(await _monitoring.GetSyncHealthAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
