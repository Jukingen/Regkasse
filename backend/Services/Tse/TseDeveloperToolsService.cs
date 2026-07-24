using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development-only TSE DX tools: diagnostics, synthetic probe traffic, config validation, non-fiscal seeds.
/// </summary>
public sealed class TseDeveloperToolsService : ITseDeveloperToolsService
{
    public const int MinTrafficCount = 1;
    public const int MaxTrafficCount = 1000;
    public const int DefaultTrafficCount = 10;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ITseIncidentService _incidents;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TseDeveloperToolsService> _logger;

    public TseDeveloperToolsService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ITseIncidentService incidents,
        IAuditLogService auditLog,
        IHostEnvironment environment,
        ILogger<TseDeveloperToolsService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _incidents = incidents;
        _auditLog = auditLog;
        _environment = environment;
        _logger = logger;
    }

    public bool IsEnabled => _environment.IsDevelopment();

    public Task<TseDeveloperToolsAvailabilityDto> GetAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        var enabled = IsEnabled;
        return Task.FromResult(new TseDeveloperToolsAvailabilityDto
        {
            Enabled = enabled,
            EnvironmentName = _environment.EnvironmentName,
            Message = enabled
                ? "TSE developer tools are available in Development."
                : "TSE developer tools are only available in Development.",
        });
    }

    public async Task<TseDevToolResultDto> RunDiagnosticsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var opts = _tseOptions.CurrentValue;
        var checks = new List<TseDevToolCheckDto>();
        var now = DateTime.UtcNow;

        checks.Add(Ok("Tenant", $"Tenant '{tenant.Name}' ({tenant.Slug}) is reachable."));

        if (devices.Count == 0)
        {
            checks.Add(Fail("Devices", "No active TSE devices for this tenant.", "Error"));
        }
        else
        {
            checks.Add(Ok("Devices", $"{devices.Count} active TSE device(s) found."));
            if (devices.Any(d => d.IsPrimary))
                checks.Add(Ok("Primary", "At least one primary device is registered."));
            else
                checks.Add(Warn("Primary", "No device marked as primary."));

            var backups = devices.Count(d => d.IsBackup);
            checks.Add(Ok("Backup", backups > 0
                ? $"{backups} backup device(s) registered."
                : "No backup devices (failover coverage may be limited)."));

            var stale = devices.Where(d =>
                    d.LastHealthCheck is null || d.LastHealthCheck < now.AddHours(-24))
                .ToList();
            if (stale.Count == 0)
                checks.Add(Ok("HealthFreshness", "All devices checked within the last 24 hours."));
            else
                checks.Add(Warn(
                    "HealthFreshness",
                    $"{stale.Count} device(s) have stale or missing health checks."));

            var hardFail = devices
                .Where(d => d.HealthStatus is TseHealthStatus.Offline
                    or TseHealthStatus.Unhealthy
                    or TseHealthStatus.Expired
                    or TseHealthStatus.Revoked)
                .ToList();
            if (hardFail.Count == 0)
                checks.Add(Ok("HealthStatus", "No Offline/Unhealthy/Expired/Revoked devices."));
            else
                checks.Add(Fail(
                    "HealthStatus",
                    $"{hardFail.Count} device(s) in hard-fail health state: "
                    + string.Join(", ", hardFail.Select(d => d.SerialNumber)),
                    "Error"));

            var avg = devices.Average(d => d.HealthScore);
            checks.Add(avg >= 50
                ? Ok("HealthScore", $"Average health score is {avg:0.#}.")
                : Fail("HealthScore", $"Average health score is low ({avg:0.#}).", "Error"));
        }

        checks.Add(Ok(
            "TseOptions",
            $"Mode={opts.Mode}, TseMode={opts.TseMode}, OfflineMode={opts.OfflineModeEnabled}, "
            + $"MaxOffline={opts.MaxOfflineTransactionsPerCashRegister}."));

        if (opts.IsOff && devices.Count > 0)
            checks.Add(Warn("TseModeOff", "TseMode=Off while devices exist — payments may skip TSE."));

        var success = checks.All(c => c.Severity != "Error");

        var result = BuildResult(
            tenantId,
            tenant.Name,
            "Diagnostics",
            success,
            success
                ? $"Diagnostics completed with {checks.Count} check(s)."
                : $"Diagnostics found {checks.Count(c => c.Severity == "Error")} error(s).",
            checks,
            new Dictionary<string, string>
            {
                ["deviceCount"] = devices.Count.ToString(),
            });

        await TryAuditAsync(
                "TSE_DX_DIAGNOSTICS",
                tenantId,
                actorUserId: "system",
                result,
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    public async Task<TseDevToolResultDto> SimulateTrafficAsync(
        Guid tenantId,
        int transactionCount,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var count = ClampTraffic(transactionCount);
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var checks = new List<TseDevToolCheckDto>();

        if (devices.Count == 0)
        {
            checks.Add(Fail("Devices", "Cannot simulate traffic without active TSE devices.", "Error"));
            return BuildResult(
                tenantId,
                tenant.Name,
                "SimulateTraffic",
                false,
                "No active devices.",
                checks);
        }

        var now = DateTime.UtcNow;
        var samples = new List<TseDeviceHealthSample>(count);
        for (var i = 0; i < count; i++)
        {
            var device = devices[i % devices.Count];
            var score = 55 + ((i * 7) % 46); // 55–100
            var status = score >= 80
                ? TseHealthStatus.Healthy
                : score >= 50
                    ? TseHealthStatus.Degraded
                    : TseHealthStatus.Unhealthy;
            var responseMs = 40 + ((i * 37) % 960);

            samples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TenantId = tenantId,
                CheckedAtUtc = now.AddSeconds(-(count - i) * 3),
                HealthScore = score,
                HealthStatus = status,
                Message = $"DX traffic sample #{i + 1} (non-fiscal)",
                IsPrimary = device.IsPrimary,
                IsBackup = device.IsBackup,
                ResponseTimeMs = responseMs,
            });
        }

        _db.TseDeviceHealthSamples.AddRange(samples);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        checks.Add(Ok(
            "HealthSamples",
            $"Inserted {samples.Count} synthetic health sample(s) across {devices.Count} device(s)."));
        checks.Add(Ok(
            "FiscalGuard",
            "No PaymentDetails, receipts, or signature-chain rows were written."));

        var result = BuildResult(
            tenantId,
            tenant.Name,
            "SimulateTraffic",
            true,
            $"Simulated {samples.Count} probe traffic sample(s).",
            checks,
            new Dictionary<string, string>
            {
                ["requestedCount"] = transactionCount.ToString(),
                ["insertedCount"] = samples.Count.ToString(),
                ["deviceCount"] = devices.Count.ToString(),
            });

        await TryAuditAsync("TSE_DX_SIMULATE_TRAFFIC", tenantId, actorUserId, result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<TseDevToolResultDto> ValidateConfigAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var opts = _tseOptions.CurrentValue;
        var checks = new List<TseDevToolCheckDto>();

        var modeOk = opts.IsFakeSigningMode
                     || string.Equals(opts.Mode, "Real", StringComparison.OrdinalIgnoreCase);
        checks.Add(modeOk
            ? Ok("Mode", $"Signing Mode='{opts.Mode}' is recognized.")
            : Fail("Mode", $"Unknown Mode='{opts.Mode}'. Expected Fake or Real.", "Error"));

        var tseModeOk = opts.IsOff
                        || opts.UseSoftTseWhenNoDevice
                        || string.Equals(opts.TseMode, "Device", StringComparison.OrdinalIgnoreCase);
        checks.Add(tseModeOk
            ? Ok("TseMode", $"TseMode='{opts.TseMode}' is recognized.")
            : Fail("TseMode", $"Unknown TseMode='{opts.TseMode}'. Expected Off, Demo, or Device.", "Error"));

        if (opts.FailoverHealthyMinScore > opts.FailoverDegradedMinScore
            && opts.FailoverDegradedMinScore is >= 0 and <= 100
            && opts.FailoverHealthyMinScore is >= 0 and <= 100)
        {
            checks.Add(Ok(
                "FailoverScores",
                $"Healthy≥{opts.FailoverHealthyMinScore}, Degraded≥{opts.FailoverDegradedMinScore}."));
        }
        else
        {
            checks.Add(Fail(
                "FailoverScores",
                "FailoverHealthyMinScore must be greater than FailoverDegradedMinScore (0–100).",
                "Error"));
        }

        if (opts.MaxOfflineTransactionsPerCashRegister is >= 1 and <= 500)
            checks.Add(Ok(
                "OfflineCap",
                $"MaxOfflineTransactionsPerCashRegister={opts.MaxOfflineTransactionsPerCashRegister}."));
        else
            checks.Add(Fail(
                "OfflineCap",
                "MaxOfflineTransactionsPerCashRegister should be between 1 and 500.",
                "Error"));

        if (opts.SlaTargetUptimePercent is > 0 and <= 100
            && opts.SlaTargetResponseTimeMs > 0
            && opts.SlaTargetSuccessRatePercent is > 0 and <= 100)
        {
            checks.Add(Ok(
                "SlaTargets",
                $"Uptime≥{opts.SlaTargetUptimePercent}%, latency≤{opts.SlaTargetResponseTimeMs}ms, "
                + $"success≥{opts.SlaTargetSuccessRatePercent}%."));
        }
        else
        {
            checks.Add(Warn("SlaTargets", "One or more SLA targets look invalid or unset."));
        }

        if (string.Equals(opts.TseMode, "Device", StringComparison.OrdinalIgnoreCase)
            && string.Equals(opts.Mode, "Real", StringComparison.OrdinalIgnoreCase)
            && devices.Any(d => string.IsNullOrWhiteSpace(d.Provider) && string.IsNullOrWhiteSpace(d.DeviceType)))
        {
            checks.Add(Fail(
                "DeviceProvider",
                "Device mode with Real signing requires Provider/DeviceType on devices.",
                "Error"));
        }
        else if (devices.Count > 0)
        {
            checks.Add(Ok(
                "DeviceProvider",
                "Active devices have provider/type metadata."));
        }
        else
        {
            checks.Add(Warn("DeviceProvider", "No active devices to validate provider metadata."));
        }

        if (_environment.IsDevelopment() && !opts.IsFakeSigningMode)
            checks.Add(Warn(
                "DevSigningHint",
                "Development host with Mode≠Fake — prefer Fake signing for local DX."));

        var success = checks.All(c => c.Severity != "Error");
        var result = BuildResult(
            tenantId,
            tenant.Name,
            "ValidateConfig",
            success,
            success ? "Configuration looks valid." : "Configuration validation failed.",
            checks);

        await TryAuditAsync("TSE_DX_VALIDATE_CONFIG", tenantId, "system", result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<TseDevToolResultDto> GenerateTestDataAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var checks = new List<TseDevToolCheckDto>();
        var now = DateTime.UtcNow;
        var sampleCount = 0;

        var targetDevices = devices.Take(3).ToList();
        if (targetDevices.Count == 0)
        {
            checks.Add(Warn(
                "HealthSamples",
                "No active devices — skipped health sample seed."));
        }
        else
        {
            foreach (var device in targetDevices)
            {
                for (var i = 0; i < 5; i++)
                {
                    _db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = device.Id,
                        TenantId = tenantId,
                        CheckedAtUtc = now.AddMinutes(-(i + 1) * 15),
                        HealthScore = 90 - (i * 5),
                        HealthStatus = i < 3 ? TseHealthStatus.Healthy : TseHealthStatus.Degraded,
                        Message = $"DX seed sample #{i + 1}",
                        IsPrimary = device.IsPrimary,
                        IsBackup = device.IsBackup,
                        ResponseTimeMs = 80 + (i * 25),
                    });
                    sampleCount++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            checks.Add(Ok(
                "HealthSamples",
                $"Seeded {sampleCount} health sample(s) on {targetDevices.Count} device(s)."));
        }

        Guid? incidentId = null;
        try
        {
            var incident = await _incidents.CreateIncidentAsync(
                    new CreateTseIncidentRequestDto
                    {
                        TenantId = tenantId,
                        DeviceId = targetDevices.FirstOrDefault()?.Id,
                        Title = "DX seed incident (non-fiscal)",
                        Description =
                            "Auto-generated by TSE developer tools for UI/ops testing. "
                            + "Not a real outage. Safe to resolve/close.",
                        Severity = "Low",
                        DetectedAt = now,
                    },
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
            incidentId = incident.Id;
            checks.Add(Ok("Incident", $"Created sample incident {incident.Id:D}."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DX GenerateTestData could not create incident for {TenantId}", tenantId);
            checks.Add(Warn("Incident", $"Incident seed skipped: {ex.Message}"));
        }

        checks.Add(Ok(
            "FiscalGuard",
            "Seeded operational data only — no fiscal receipts or signature-chain mutations."));

        var result = BuildResult(
            tenantId,
            tenant.Name,
            "GenerateTestData",
            true,
            $"Test data generated (samples={sampleCount}, incident={(incidentId?.ToString("D") ?? "none")}).",
            checks,
            new Dictionary<string, string>
            {
                ["healthSampleCount"] = sampleCount.ToString(),
                ["incidentId"] = incidentId?.ToString("D") ?? string.Empty,
            });

        await TryAuditAsync("TSE_DX_GENERATE_TEST_DATA", tenantId, actorUserId, result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
            throw new InvalidOperationException("TSE developer tools are only available in Development.");
    }

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private Task<List<TseDevice>> LoadActiveDevicesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.TseDevices.AsNoTracking()
            .Where(d => d.IsActive && d.TenantId == tenantId)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.SerialNumber)
            .ToListAsync(cancellationToken);

    private static int ClampTraffic(int count) =>
        Math.Clamp(count <= 0 ? DefaultTrafficCount : count, MinTrafficCount, MaxTrafficCount);

    private static TseDevToolResultDto BuildResult(
        Guid tenantId,
        string? tenantName,
        string operation,
        bool success,
        string summary,
        IReadOnlyList<TseDevToolCheckDto> checks,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new()
        {
            TenantId = tenantId,
            TenantName = tenantName,
            Operation = operation,
            Success = success,
            Summary = summary,
            GeneratedAtUtc = DateTime.UtcNow,
            DevelopmentOnly = true,
            Results = checks,
            Metadata = metadata,
        };

    private static TseDevToolCheckDto Ok(string name, string details) =>
        new()
        {
            Name = name,
            IsSuccess = true,
            Details = details,
            Severity = "Info",
        };

    private static TseDevToolCheckDto Warn(string name, string details) =>
        new()
        {
            Name = name,
            IsSuccess = true,
            Details = details,
            Severity = "Warning",
        };

    private static TseDevToolCheckDto Fail(string name, string details, string severity) =>
        new()
        {
            Name = name,
            IsSuccess = false,
            Details = details,
            Severity = severity,
        };

    private async Task TryAuditAsync(
        string action,
        Guid tenantId,
        string? actorUserId,
        TseDevToolResultDto result,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                    action,
                    "TseDeveloperTools",
                    userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim(),
                    userRole: "SuperAdmin",
                    description: $"TSE DX: {result.Operation} — {result.Summary}",
                    status: result.Success ? AuditLogStatus.Success : AuditLogStatus.Failed,
                    responseData: new
                    {
                        result.TenantId,
                        result.Operation,
                        result.Success,
                        result.Summary,
                        CheckCount = result.Results.Count,
                        result.Metadata,
                    },
                    tenantId: tenantId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit failed for TSE DX action {Action}", action);
        }
    }
}
