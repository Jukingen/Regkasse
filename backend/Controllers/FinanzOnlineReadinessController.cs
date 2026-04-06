using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: FinanzOnline configuration readiness (simulation vs real TEST, missing endpoints, outbox worker). No credentials or SOAP calls.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/finanzonline-readiness")]
[Produces("application/json")]
public sealed class FinanzOnlineReadinessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _session;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassen;
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _transmissionQuery;
    private readonly IOptionsMonitor<FinanzOnlineOutboxOptions> _outbox;
    private readonly IOptionsMonitor<FinanzOnlineConnectivityOptions> _connectivity;
    private readonly IOptionsMonitor<FinanzOnlineDevTestOptions> _devTest;
    private readonly IOptionsMonitor<FinanzOnlineSimulationOptions> _simulationScenario;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISettingsTenantResolver _settingsTenant;

    public FinanzOnlineReadinessController(
        AppDbContext db,
        IOptionsMonitor<FinanzOnlineSessionOptions> session,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassen,
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> transmissionQuery,
        IOptionsMonitor<FinanzOnlineOutboxOptions> outbox,
        IOptionsMonitor<FinanzOnlineConnectivityOptions> connectivity,
        IOptionsMonitor<FinanzOnlineDevTestOptions> devTest,
        IOptionsMonitor<FinanzOnlineSimulationOptions> simulationScenario,
        IHostEnvironment hostEnvironment,
        ISettingsTenantResolver settingsTenant)
    {
        _db = db;
        _session = session;
        _registrierkassen = registrierkassen;
        _transmissionQuery = transmissionQuery;
        _outbox = outbox;
        _connectivity = connectivity;
        _devTest = devTest;
        _simulationScenario = simulationScenario;
        _hostEnvironment = hostEnvironment;
        _settingsTenant = settingsTenant;
    }

    /// <summary>
    /// GET: Configuration readiness plus outbox row counts by status (observability).
    /// </summary>
    [HttpGet]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    public async Task<ActionResult<FinanzOnlineReadinessResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        FinanzOnlineReadinessTenantCompanyProbe? tenantProbe = null;
        if (_connectivity.CurrentValue.UseCompanySettings)
        {
            tenantProbe = await BuildTenantCompanyProbeAsync(cancellationToken).ConfigureAwait(false);
        }

        var baseline = FinanzOnlineReadinessEvaluator.Evaluate(
            _session.CurrentValue,
            _registrierkassen.CurrentValue,
            _transmissionQuery.CurrentValue,
            _outbox.CurrentValue,
            _connectivity.CurrentValue,
            _devTest.CurrentValue,
            _simulationScenario.CurrentValue,
            _hostEnvironment,
            tenantProbe);

        var counts = await _db.FinanzOnlineOutboxMessages.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dict = counts.ToDictionary(x => x.Key ?? "", x => x.Count, StringComparer.Ordinal);

        return Ok(new FinanzOnlineReadinessResponse
        {
            OverallStatus = baseline.OverallStatus,
            TransportMode = baseline.TransportMode,
            RealTestSubmissionPossible = baseline.RealTestSubmissionPossible,
            ProtocolReconciliationPossible = baseline.ProtocolReconciliationPossible,
            OutboxWorkerEnabled = baseline.OutboxWorkerEnabled,
            Summary = baseline.Summary,
            Findings = baseline.Findings,
            OutboxCountsByStatus = dict,
            ConfiguredSimulationScenario = baseline.ConfiguredSimulationScenario,
            EffectiveSimulationScenario = baseline.EffectiveSimulationScenario,
            SimulationFixedDelayMs = baseline.SimulationFixedDelayMs,
            SimulationSeed = baseline.SimulationSeed,
            Diagnostics = baseline.Diagnostics,
        });
    }

    private async Task<FinanzOnlineReadinessTenantCompanyProbe> BuildTenantCompanyProbeAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenant.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var row = await _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
        {
            return new FinanzOnlineReadinessTenantCompanyProbe
            {
                WasEvaluated = true,
                CompanySettingsRowExists = false,
            };
        }

        return new FinanzOnlineReadinessTenantCompanyProbe
        {
            WasEvaluated = true,
            CompanySettingsRowExists = true,
            HasFinanzOnlineApiUrl = !string.IsNullOrWhiteSpace(row.FinanzOnlineApiUrl),
            HasFinanzOnlineUsername = !string.IsNullOrWhiteSpace(row.FinanzOnlineUsername),
            HasFinanzOnlinePassword = !string.IsNullOrWhiteSpace(row.FinanzOnlinePassword),
            HasFinanzOnlineTelematikId = !string.IsNullOrWhiteSpace(row.FinanzOnlineTelematikId),
            HasFinanzOnlineHerstellerId = !string.IsNullOrWhiteSpace(row.FinanzOnlineHerstellerId),
        };
    }
}
