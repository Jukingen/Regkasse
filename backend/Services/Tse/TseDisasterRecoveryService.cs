using System.Diagnostics;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Generates and safely executes TSE DR runbooks (simulation checks against live inventory/health).
/// Does not trigger production failover or restore.
/// </summary>
public sealed class TseDisasterRecoveryService : ITseDisasterRecoveryService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ITseDeviceHealthCheckService _healthCheck;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseDisasterRecoveryService> _logger;

    public TseDisasterRecoveryService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ITseDeviceHealthCheckService healthCheck,
        IActivityEventPublisher activity,
        ILogger<TseDisasterRecoveryService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _healthCheck = healthCheck;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseDrRunbookDto> GenerateRunbookAsync(
        Guid tenantId,
        string scenario = TseDrScenarios.TseFailure,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var normalized = NormalizeScenario(scenario);
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var rtoTarget = Math.Clamp(_tseOptions.CurrentValue.DrRtoTargetMinutes, 5, 240);

        var runbook = new TseDrRunbook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"{normalized} runbook — {tenant.Name}",
            Scenario = normalized,
            Status = TseDrRunbookStatuses.Ready,
            EstimatedRtoMinutes = rtoTarget,
            ActualRtoMinutes = 0,
            IsDrill = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Truncate(actorUserId, 450),
            Summary = "Simulation runbook. Automated steps validate readiness; they do not perform live failover.",
        };

        foreach (var step in BuildSteps(normalized))
            runbook.Steps.Add(step);

        _db.TseDrRunbooks.Add(runbook);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        _logger.LogInformation(
            "Generated TSE DR runbook RunbookId={RunbookId} TenantId={TenantId} Scenario={Scenario}",
            runbook.Id,
            tenantId,
            normalized);

        return await MapRunbookAsync(runbook.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseDrExecutionResultDto> ExecuteRunbookAsync(
        Guid runbookId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (runbookId == Guid.Empty)
            throw new ArgumentException("runbookId is required.", nameof(runbookId));

        var runbook = await _db.TseDrRunbooks
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == runbookId, cancellationToken)
            .ConfigureAwait(false);
        if (runbook is null)
            throw new KeyNotFoundException($"Runbook {runbookId} was not found.");

        if (string.Equals(runbook.Status, TseDrRunbookStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Runbook is already in progress.");

        var sw = Stopwatch.StartNew();
        runbook.Status = TseDrRunbookStatuses.InProgress;
        runbook.StartedAt = DateTime.UtcNow;
        runbook.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var completed = 0;
        var failed = 0;
        var skippedManual = 0;

        foreach (var step in runbook.Steps.OrderBy(s => s.Order))
        {
            step.StartedAt = DateTime.UtcNow;
            step.Error = null;
            step.Result = null;
            step.IsCompleted = false;

            try
            {
                if (!step.IsAutomated)
                {
                    skippedManual++;
                    step.Result = "Manual step — requires operator confirmation (simulation skipped).";
                    step.IsCompleted = false;
                    step.CompletedAt = DateTime.UtcNow;
                    continue;
                }

                var (ok, result) = await ExecuteAutomatedStepAsync(runbook, step, cancellationToken)
                    .ConfigureAwait(false);
                step.Result = result;
                step.IsCompleted = ok;
                step.CompletedAt = DateTime.UtcNow;
                if (ok)
                    completed++;
                else
                {
                    failed++;
                    step.Error = result;
                }
            }
            catch (Exception ex)
            {
                failed++;
                step.IsCompleted = false;
                step.Error = Truncate(ex.Message, 2000);
                step.Result = "Step failed.";
                step.CompletedAt = DateTime.UtcNow;
                _logger.LogWarning(ex, "TSE DR step failed RunbookId={RunbookId} Action={Action}", runbook.Id, step.Action);
            }
        }

        sw.Stop();
        var actualRto = Math.Max(1, (int)Math.Ceiling(Math.Max(sw.Elapsed.TotalMinutes, 0.01)));

        var success = failed == 0;
        runbook.Status = success ? TseDrRunbookStatuses.Completed : TseDrRunbookStatuses.Failed;
        runbook.ActualRtoMinutes = actualRto;
        runbook.CompletedAt = DateTime.UtcNow;
        runbook.LastTestedAt = DateTime.UtcNow;
        runbook.UpdatedAt = DateTime.UtcNow;
        runbook.Summary = success
            ? $"Simulation completed: {completed} automated steps ok, {skippedManual} manual pending."
            : $"Simulation finished with failures: {failed} failed, {completed} ok, {skippedManual} manual pending.";

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        var mapped = await MapRunbookAsync(runbookId, cancellationToken).ConfigureAwait(false);
        return new TseDrExecutionResultDto
        {
            RunbookId = runbookId,
            TenantId = mapped.TenantId,
            Status = mapped.Status,
            Success = success,
            ActualRtoMinutes = actualRto,
            CompletedSteps = completed,
            FailedSteps = failed,
            SkippedManualSteps = skippedManual,
            Message = mapped.Summary ?? string.Empty,
            SimulationOnly = true,
            Runbook = mapped,
        };
    }

    public async Task<TseDrStatusDto> GetDrStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var opts = _tseOptions.CurrentValue;
        var rtoTarget = Math.Clamp(opts.DrRtoTargetMinutes, 5, 240);

        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var primaries = devices.Where(d => d.IsPrimary || d.IsFailoverActive).ToList();
        var healthyBackups = devices.Count(d =>
            d.IsBackup
            && !d.IsFailoverActive
            && d.HealthStatus is TseHealthStatus.Healthy or TseHealthStatus.Degraded);

        var latest = await _db.TseDrRunbooks.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.LastTestedAt, r.ActualRtoMinutes })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastDrill = await _db.TseDrRunbooks.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsDrill && r.LastTestedAt != null)
            .OrderByDescending(r => r.LastTestedAt)
            .Select(r => new { r.LastTestedAt, r.ActualRtoMinutes, r.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var isReady = primaries.Count > 0
                      && healthyBackups >= Math.Max(0, opts.DrMinHealthyBackups)
                      && (lastDrill?.LastTestedAt is null
                          || lastDrill.LastTestedAt >= DateTime.UtcNow.AddDays(-Math.Clamp(opts.DrMaxDrillAgeDays, 7, 365)));

        // If never drilled, still "ready" only when inventory OK — mark messaging accordingly.
        if (lastDrill is null)
            isReady = primaries.Count > 0 && healthyBackups >= Math.Max(0, opts.DrMinHealthyBackups);

        var message = isReady
            ? "DR inventory ready (simulation). Schedule periodic drills to validate RTO."
            : BuildNotReadyMessage(primaries.Count, healthyBackups, opts.DrMinHealthyBackups, lastDrill?.LastTestedAt);

        TseDrRunbookDto? latestDto = null;
        if (latest is not null)
            latestDto = await MapRunbookAsync(latest.Id, cancellationToken).ConfigureAwait(false);

        return new TseDrStatusDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            IsReady = isReady,
            LastDrillAt = lastDrill?.LastTestedAt,
            RtoTargetMinutes = rtoTarget,
            RtoActualMinutes = lastDrill?.ActualRtoMinutes ?? latest?.ActualRtoMinutes ?? 0,
            PrimaryDeviceCount = primaries.Count,
            HealthyBackupCount = healthyBackups,
            ReadinessMessage = message,
            LatestRunbookId = latest?.Id,
            LatestRunbook = latestDto,
        };
    }

    public async Task<TseDrReportDto> RunDrDrillAsync(
        Guid tenantId,
        string scenario = TseDrScenarios.TseFailure,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var runbook = await GenerateRunbookAsync(tenantId, scenario, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        // Mark as drill before execute.
        var entity = await _db.TseDrRunbooks.FirstAsync(r => r.Id == runbook.Id, cancellationToken)
            .ConfigureAwait(false);
        entity.IsDrill = true;
        entity.Name = $"[DRILL] {entity.Name}";
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        var execution = await ExecuteRunbookAsync(runbook.Id, actorUserId, cancellationToken)
            .ConfigureAwait(false);
        var statusAfter = await GetDrStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var completed = DateTime.UtcNow;
        var rtoTarget = statusAfter.RtoTargetMinutes;
        var findings = new List<string>();

        if (execution.FailedSteps > 0)
            findings.Add($"{execution.FailedSteps} automated step(s) failed during simulation.");
        if (execution.SkippedManualSteps > 0)
            findings.Add($"{execution.SkippedManualSteps} manual step(s) require operator follow-up.");
        if (statusAfter.HealthyBackupCount < _tseOptions.CurrentValue.DrMinHealthyBackups)
            findings.Add("Healthy backup inventory below configured minimum.");
        if (execution.ActualRtoMinutes > rtoTarget)
            findings.Add($"Simulation RTO {execution.ActualRtoMinutes} min exceeded target {rtoTarget} min.");
        if (findings.Count == 0)
            findings.Add("Drill simulation completed within readiness expectations.");

        await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.TseDrDrillCompleted,
                new
                {
                    TenantId = tenantId.ToString("D"),
                    RunbookId = runbook.Id.ToString("D"),
                    Scenario = execution.Runbook.Scenario,
                    execution.Success,
                    execution.ActualRtoMinutes,
                    RtoTargetMinutes = rtoTarget,
                    SimulationOnly = true,
                },
                actorUserId: actorUserId ?? "system",
                dedupKey: $"tse-dr-drill:{tenantId:N}:{runbook.Id:N}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new TseDrReportDto
        {
            TenantId = tenantId,
            RunbookId = runbook.Id,
            Scenario = execution.Runbook.Scenario,
            Success = execution.Success,
            StartedAt = started,
            CompletedAt = completed,
            ActualRtoMinutes = execution.ActualRtoMinutes,
            RtoTargetMinutes = rtoTarget,
            MetRtoTarget = execution.ActualRtoMinutes <= rtoTarget,
            Summary = execution.Message,
            Findings = findings,
            Execution = execution,
            StatusAfter = statusAfter,
        };
    }

    private async Task<(bool Ok, string Result)> ExecuteAutomatedStepAsync(
        TseDrRunbook runbook,
        TseDrStep step,
        CancellationToken cancellationToken)
    {
        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == runbook.TenantId && d.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return step.Action switch
        {
            "InventoryPrimaries" => InventoryPrimaries(devices),
            "InventoryBackups" => InventoryBackups(devices),
            "HealthCheckPrimary" => await HealthCheckPrimaryAsync(devices, cancellationToken).ConfigureAwait(false),
            "HealthCheckBackup" => await HealthCheckBackupAsync(devices, cancellationToken).ConfigureAwait(false),
            "ValidateFailoverPath" => ValidateFailoverPath(devices),
            "CheckOfflineQueue" => await CheckOfflineQueueAsync(runbook.TenantId, cancellationToken).ConfigureAwait(false),
            "CheckConnectivityFlags" => CheckConnectivityFlags(devices),
            "VerifySignatureChainSample" => await VerifySignatureChainSampleAsync(runbook.TenantId, cancellationToken)
                .ConfigureAwait(false),
            "LocateRecentBackup" => await LocateRecentBackupAsync(runbook.TenantId, cancellationToken)
                .ConfigureAwait(false),
            "SimulateFailoverPlan" => SimulateFailoverPlan(devices),
            _ => (true, $"No-op simulation for action '{step.Action}'."),
        };
    }

    private static (bool, string) InventoryPrimaries(IReadOnlyList<TseDevice> devices)
    {
        var count = devices.Count(d => d.IsPrimary || d.IsFailoverActive);
        return count > 0
            ? (true, $"Found {count} active signing primary/failover device(s).")
            : (false, "No active primary/failover-active TSE device found.");
    }

    private static (bool, string) InventoryBackups(IReadOnlyList<TseDevice> devices)
    {
        var count = devices.Count(d => d.IsBackup);
        return count > 0
            ? (true, $"Found {count} backup device(s).")
            : (false, "No backup TSE device configured.");
    }

    private async Task<(bool, string)> HealthCheckPrimaryAsync(
        IReadOnlyList<TseDevice> devices,
        CancellationToken cancellationToken)
    {
        var primary = devices.FirstOrDefault(d => d.IsPrimary) ?? devices.FirstOrDefault(d => d.IsFailoverActive);
        if (primary is null)
            return (false, "No primary device to health-check.");

        var result = await _healthCheck.CheckHealthAsync(primary.Id, cancellationToken).ConfigureAwait(false);
        return (true, $"Primary {primary.Id:D} health={result.Status}, score={result.HealthScore}.");
    }

    private async Task<(bool, string)> HealthCheckBackupAsync(
        IReadOnlyList<TseDevice> devices,
        CancellationToken cancellationToken)
    {
        var backup = devices.FirstOrDefault(d => d.IsBackup && !d.IsFailoverActive);
        if (backup is null)
            return (false, "No idle backup device to health-check.");

        var result = await _healthCheck.CheckHealthAsync(backup.Id, cancellationToken).ConfigureAwait(false);
        var ok = result.Status is TseHealthStatus.Healthy or TseHealthStatus.Degraded;
        return ok
            ? (true, $"Backup {backup.Id:D} health={result.Status}, score={result.HealthScore}.")
            : (false, $"Backup {backup.Id:D} is not healthy enough for failover (status={result.Status}).");
    }

    private static (bool, string) ValidateFailoverPath(IReadOnlyList<TseDevice> devices)
    {
        var primary = devices.FirstOrDefault(d => d.IsPrimary);
        var backup = devices.FirstOrDefault(d =>
            d.IsBackup
            && !d.IsFailoverActive
            && d.HealthStatus is TseHealthStatus.Healthy or TseHealthStatus.Degraded);

        if (primary is null)
            return (false, "Failover path invalid: missing primary.");
        if (backup is null)
            return (false, "Failover path invalid: no healthy idle backup.");
        if (primary.CashRegisterId != Guid.Empty
            && backup.CashRegisterId != Guid.Empty
            && primary.CashRegisterId != backup.CashRegisterId
            && primary.TenantId != backup.TenantId)
            return (false, "Failover path mismatch across registers/tenants.");

        return (true, $"Simulation failover path OK: primary {primary.Id:D} → backup {backup.Id:D} (not activated).");
    }

    private async Task<(bool, string)> CheckOfflineQueueAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var pending = await _db.OfflineTransactions.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                t => t.TenantId == tenantId && t.Status == OfflineTransactionStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, $"Offline queue pending intents ≈ {pending}.");
    }

    private static (bool, string) CheckConnectivityFlags(IReadOnlyList<TseDevice> devices)
    {
        var offline = devices.Count(d => !d.IsConnected || d.HealthStatus == TseHealthStatus.Offline);
        return (true, $"{offline}/{devices.Count} device(s) currently disconnected or offline.");
    }

    private async Task<(bool, string)> VerifySignatureChainSampleAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var recent = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.IssuedAt)
            .Take(20)
            .Select(r => r.SignatureValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (recent.Count == 0)
            return (true, "No recent receipts to sample for signature presence.");

        var signed = recent.Count(s => !string.IsNullOrWhiteSpace(s));
        var ok = signed == recent.Count;
        return ok
            ? (true, $"Signature sample OK: {signed}/{recent.Count} recent receipts signed.")
            : (false, $"Signature gaps detected: {signed}/{recent.Count} recent receipts signed.");
    }

    private async Task<(bool, string)> LocateRecentBackupAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var recent = await _db.TseBackups.AsNoTracking().IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.Id, b.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (recent is not null)
            return (true, $"Latest TSE backup {recent.Id:D} at {recent.CreatedAt:u}.");

        var run = await _db.BackupRuns.AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.RequestedAt)
            .Select(b => new { b.Id, b.RequestedAt, b.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return run is null
            ? (false, "No backup artifacts located for tenant.")
            : (true, $"Latest backup run {run.Id:D} status={run.Status} at {run.RequestedAt:u}.");
    }

    private static (bool, string) SimulateFailoverPlan(IReadOnlyList<TseDevice> devices)
    {
        var path = ValidateFailoverPath(devices);
        if (!path.Item1)
            return path;
        return (true, "Failover plan simulated only — live ManualFailoverAsync was NOT invoked.");
    }

    private static List<TseDrStep> BuildSteps(string scenario)
    {
        var steps = new List<(string Action, string Description, bool Auto)>
        {
            ("InventoryPrimaries", "Enumerate active primary / failover-active TSE devices.", true),
            ("InventoryBackups", "Enumerate configured backup TSE devices.", true),
        };

        switch (scenario)
        {
            case TseDrScenarios.NetworkIsolation:
                steps.Add(("CheckConnectivityFlags", "Inspect device connectivity / offline flags.", true));
                steps.Add(("CheckOfflineQueue", "Review offline queue depth for backlog risk.", true));
                steps.Add(("HealthCheckBackup", "Confirm backup remains reachable for failover.", true));
                steps.Add(("ValidateFailoverPath", "Validate primary→backup pairing for simulation.", true));
                steps.Add(("NotifyOperators", "Notify on-call operators of network isolation DR status.", false));
                break;
            case TseDrScenarios.DataCorruption:
                steps.Add(("VerifySignatureChainSample", "Sample recent receipts for missing TSE signatures.", true));
                steps.Add(("LocateRecentBackup", "Locate latest TSE/tenant backup artifact.", true));
                steps.Add(("HealthCheckPrimary", "Re-check primary device health after integrity sample.", true));
                steps.Add(("QuarantineSuspectDevice", "Operator quarantines suspect device from signing.", false));
                steps.Add(("RestoreValidationOnly", "Operator starts validation-only restore drill (no production restore).", false));
                break;
            default:
                steps.Add(("HealthCheckPrimary", "Probe primary TSE device health.", true));
                steps.Add(("HealthCheckBackup", "Probe backup TSE device health.", true));
                steps.Add(("ValidateFailoverPath", "Validate simulated failover path.", true));
                steps.Add(("SimulateFailoverPlan", "Record failover plan without activating backup.", true));
                steps.Add(("ActivateBackupManual", "Operator confirms live failover when approved.", false));
                steps.Add(("PostFailoverVerification", "Operator verifies signing resumes on backup.", false));
                break;
        }

        return steps.Select((s, i) => new TseDrStep
        {
            Id = Guid.NewGuid(),
            Order = i + 1,
            Action = s.Action,
            Description = s.Description,
            IsAutomated = s.Auto,
        }).ToList();
    }

    private async Task<TseDrRunbookDto> MapRunbookAsync(Guid runbookId, CancellationToken cancellationToken)
    {
        var runbook = await _db.TseDrRunbooks.AsNoTracking()
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == runbookId, cancellationToken)
            .ConfigureAwait(false);
        if (runbook is null)
            throw new KeyNotFoundException($"Runbook {runbookId} was not found.");

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == runbook.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return new TseDrRunbookDto
        {
            Id = runbook.Id,
            TenantId = runbook.TenantId,
            TenantName = tenant?.Name,
            Name = runbook.Name,
            Scenario = runbook.Scenario,
            Status = runbook.Status,
            CreatedAt = runbook.CreatedAt,
            LastTestedAt = runbook.LastTestedAt,
            EstimatedRtoMinutes = runbook.EstimatedRtoMinutes,
            ActualRtoMinutes = runbook.ActualRtoMinutes,
            IsDrill = runbook.IsDrill,
            Summary = runbook.Summary,
            Steps = runbook.Steps
                .OrderBy(s => s.Order)
                .Select(s => new TseDrStepDto
                {
                    Id = s.Id,
                    Order = s.Order,
                    Action = s.Action,
                    Description = s.Description,
                    IsAutomated = s.IsAutomated,
                    IsCompleted = s.IsCompleted,
                    StartedAt = s.StartedAt,
                    CompletedAt = s.CompletedAt,
                    Result = s.Result,
                    Error = s.Error,
                })
                .ToList(),
        };
    }

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private static string NormalizeScenario(string? scenario)
    {
        var raw = (scenario ?? TseDrScenarios.TseFailure).Trim();
        if (!TseDrScenarios.IsValid(raw))
            throw new ArgumentException("Scenario must be TSEFailure, NetworkIsolation, or DataCorruption.");
        return TseDrScenarios.All.First(s => string.Equals(s, raw, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildNotReadyMessage(
        int primaries,
        int healthyBackups,
        int minBackups,
        DateTime? lastDrillAt)
    {
        if (primaries <= 0)
            return "Not ready: no active primary TSE device.";
        if (healthyBackups < minBackups)
            return $"Not ready: healthy backups {healthyBackups} < required {minBackups}.";
        if (lastDrillAt is null)
            return "Inventory OK but no successful DR drill recorded yet.";
        return "Not ready: last DR drill is older than configured max age.";
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
