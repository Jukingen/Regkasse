using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Evaluates tenant TSE load vs scaling policy. Soft-provisions idle backup stubs only when
/// <see cref="TseScalingPolicy.AutoProvision"/> is true — never live cloud TSS or Startbeleg.
/// </summary>
public sealed class TseAutoScalingService : ITseAutoScalingService
{
    private const int LookbackDays = 7;
    private const int MaxHistoryTake = 200;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TseAutoScalingService> _logger;

    public TseAutoScalingService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        IHostEnvironment environment,
        ILogger<TseAutoScalingService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _activity = activity;
        _environment = environment;
        _logger = logger;
    }

    public async Task<TseScalingPolicyDto> GetScalingPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var policy = await GetOrCreatePolicyAsync(tenantId, actorUserId: null, cancellationToken)
            .ConfigureAwait(false);
        return MapPolicy(policy);
    }

    public async Task<TseScalingPolicyDto> ConfigureScalingPolicyAsync(
        Guid tenantId,
        ConfigureTseScalingPolicyRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var policy = await GetOrCreatePolicyAsync(tenantId, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        policy.Enabled = request.Enabled;
        policy.MinDevices = Math.Clamp(request.MinDevices, 1, 50);
        policy.MaxDevices = Math.Clamp(request.MaxDevices, policy.MinDevices, 50);
        policy.TargetTransactionsPerDevice = Math.Clamp(request.TargetTransactionsPerDevice, 100, 100_000);
        policy.ScaleUpThreshold = Clamp(request.ScaleUpThreshold, 50, 99);
        policy.ScaleDownThreshold = Clamp(request.ScaleDownThreshold, 5, policy.ScaleUpThreshold - 1);
        policy.CooldownMinutes = Math.Clamp(request.CooldownMinutes, 5, 24 * 60);
        policy.AutoProvision = request.AutoProvision;
        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = Truncate(actorUserId, 450);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapPolicy(policy);
    }

    public async Task<TseScalingHistoryDto> GetScalingHistoryAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        take = Math.Clamp(take, 1, MaxHistoryTake);

        var items = await _db.TseScalingHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.EvaluatedAt)
            .Take(take)
            .Select(h => new TseScalingHistoryItemDto
            {
                Id = h.Id,
                Timestamp = h.EvaluatedAt,
                Action = h.Action,
                From = h.FromDevices,
                To = h.ToDevices,
                LoadPercent = h.LoadPercent,
                Reason = h.Reason,
                Applied = h.Applied,
                SimulationOnly = h.SimulationOnly,
                ActorUserId = h.ActorUserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TseScalingHistoryDto { TenantId = tenantId, Items = items };
    }

    public async Task<TseScalingStatusDto> GetScalingStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var policy = await GetOrCreatePolicyAsync(tenantId, null, cancellationToken).ConfigureAwait(false);
        var (current, load, recommended) = await EvaluateLoadAsync(tenantId, policy, cancellationToken)
            .ConfigureAwait(false);

        var last = await _db.TseScalingHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.EvaluatedAt)
            .Select(h => new TseScalingHistoryItemDto
            {
                Id = h.Id,
                Timestamp = h.EvaluatedAt,
                Action = h.Action,
                From = h.FromDevices,
                To = h.ToDevices,
                LoadPercent = h.LoadPercent,
                Reason = h.Reason,
                Applied = h.Applied,
                SimulationOnly = h.SimulationOnly,
                ActorUserId = h.ActorUserId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TseScalingStatusDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            ScalingEnabled = policy.Enabled,
            CurrentDevices = current,
            RecommendedDevices = recommended,
            CurrentLoadPercent = load,
            Policy = MapPolicy(policy),
            LastEvaluation = last,
        };
    }

    public async Task<TseScalingResultDto> EvaluateAndScaleAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var policy = await GetOrCreatePolicyAsync(tenantId, actorUserId, cancellationToken)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var (current, load, recommended) = await EvaluateLoadAsync(tenantId, policy, cancellationToken)
            .ConfigureAwait(false);

        if (!policy.Enabled)
        {
            return await PersistResultAsync(
                    tenantId,
                    tenant.Name,
                    policy,
                    current,
                    recommended,
                    load,
                    TseScalingActions.Disabled,
                    applied: false,
                    simulationOnly: true,
                    "Auto-scaling is disabled for this tenant.",
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (await IsInCooldownAsync(tenantId, policy.CooldownMinutes, now, cancellationToken)
            .ConfigureAwait(false))
        {
            return await PersistResultAsync(
                    tenantId,
                    tenant.Name,
                    policy,
                    current,
                    recommended,
                    load,
                    TseScalingActions.SkippedCooldown,
                    applied: false,
                    simulationOnly: true,
                    $"Cooldown active ({policy.CooldownMinutes} minutes since last applied scale).",
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (recommended == current)
        {
            return await PersistResultAsync(
                    tenantId,
                    tenant.Name,
                    policy,
                    current,
                    recommended,
                    load,
                    TseScalingActions.NoOp,
                    applied: false,
                    simulationOnly: true,
                    $"Load {load:0.#}% within thresholds; keep {current} signing device(s).",
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var action = recommended > current ? TseScalingActions.ScaleUp : TseScalingActions.ScaleDown;
        var canSoftApply = policy.AutoProvision && _environment.IsDevelopment();
        var simulationOnly = !canSoftApply;
        var applied = false;
        var reason = recommended > current
            ? $"Load {load:0.#}% ≥ scale-up threshold {policy.ScaleUpThreshold:0.#}% → recommend {recommended} device(s)."
            : $"Load {load:0.#}% ≤ scale-down threshold {policy.ScaleDownThreshold:0.#}% → recommend {recommended} device(s).";

        if (canSoftApply)
        {
            try
            {
                if (recommended > current)
                {
                    applied = await SoftScaleUpAsync(tenantId, current, recommended, cancellationToken)
                        .ConfigureAwait(false);
                    reason += applied
                        ? " Soft backup stubs created (Development + AutoProvision)."
                        : " Soft scale-up skipped (no primary device to attach backups).";
                }
                else
                {
                    applied = await SoftScaleDownAsync(tenantId, current, recommended, cancellationToken)
                        .ConfigureAwait(false);
                    reason += applied
                        ? " Idle backup stubs deactivated (Development + AutoProvision)."
                        : " Soft scale-down skipped (no idle backups).";
                }

                simulationOnly = !applied;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Soft auto-scale failed for tenant {TenantId}", tenantId);
                reason += $" Soft apply failed: {ex.Message}";
                applied = false;
                simulationOnly = true;
            }
        }
        else
        {
            action = TseScalingActions.Recommend;
            reason += policy.AutoProvision
                ? " AutoProvision is on but soft apply is Development-only (no live cloud provision)."
                : " Recommendation only (AutoProvision=false).";
        }

        var result = await PersistResultAsync(
                tenantId,
                tenant.Name,
                policy,
                current,
                recommended,
                load,
                action,
                applied,
                simulationOnly,
                reason,
                actorUserId,
                cancellationToken)
            .ConfigureAwait(false);

        if (action is TseScalingActions.ScaleUp or TseScalingActions.ScaleDown or TseScalingActions.Recommend)
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TseAutoScaleRecommended,
                    new
                    {
                        TenantId = tenantId.ToString("D"),
                        Action = action,
                        CurrentDevices = current,
                        RecommendedDevices = recommended,
                        LoadPercent = load,
                        Applied = applied,
                        SimulationOnly = simulationOnly,
                        Reason = reason,
                    },
                    actorUserId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    dedupKey: $"tse-autoscale:{tenantId:N}:{DateTime.UtcNow:yyyyMMddHH}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private async Task<(int Current, double LoadPercent, int Recommended)> EvaluateLoadAsync(
        Guid tenantId,
        TseScalingPolicy policy,
        CancellationToken cancellationToken)
    {
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.Date.AddDays(-(LookbackDays - 1));

        var receiptCount = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                r => r.TenantId == tenantId && r.IssuedAt >= fromUtc && r.IssuedAt < toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var signingDevices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                d => d.TenantId == tenantId
                     && d.IsActive
                     && (d.IsPrimary || d.IsFailoverActive),
                cancellationToken)
            .ConfigureAwait(false);

        // Count active fleet including idle backups for currentDevices display/recommendation base.
        var activeDevices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(d => d.TenantId == tenantId && d.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var current = Math.Max(1, Math.Max(signingDevices, activeDevices > 0 ? activeDevices : 1));
        if (activeDevices == 0 && signingDevices == 0)
            current = 0;

        var effectiveSigning = Math.Max(1, signingDevices);
        var dailyAvg = receiptCount / (double)LookbackDays;
        var target = Math.Max(100, policy.TargetTransactionsPerDevice);
        var capacity = effectiveSigning * target;
        var load = capacity <= 0 ? 0 : Math.Round(100.0 * dailyAvg / capacity, 1);

        var recommended = current == 0 ? policy.MinDevices : current;
        if (load >= policy.ScaleUpThreshold)
        {
            var needed = (int)Math.Ceiling(dailyAvg / target);
            recommended = Math.Clamp(Math.Max(needed, current + 1), policy.MinDevices, policy.MaxDevices);
        }
        else if (load <= policy.ScaleDownThreshold && current > policy.MinDevices)
        {
            var needed = Math.Max(policy.MinDevices, (int)Math.Ceiling(dailyAvg / target));
            recommended = Math.Clamp(Math.Min(needed, current - 1), policy.MinDevices, policy.MaxDevices);
        }
        else
        {
            recommended = Math.Clamp(Math.Max(current, policy.MinDevices), policy.MinDevices, policy.MaxDevices);
        }

        // Prefer signing device count for load math display when no devices exist.
        var displayCurrent = activeDevices > 0 ? activeDevices : signingDevices;
        return (displayCurrent, load, recommended);
    }

    private async Task<bool> SoftScaleUpAsync(
        Guid tenantId,
        int from,
        int to,
        CancellationToken cancellationToken)
    {
        var need = to - from;
        if (need <= 0)
            return false;

        var primary = await _db.TseDevices
            .Where(d => d.TenantId == tenantId && d.IsActive && d.IsPrimary)
            .OrderByDescending(d => d.LastHealthCheck)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (primary is null)
            return false;

        var now = DateTime.UtcNow;
        for (var i = 0; i < need; i++)
        {
            _db.TseDevices.Add(new TseDevice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SerialNumber = $"AUTO-BACKUP-{now:yyyyMMddHHmmss}-{i + 1}",
                DeviceType = primary.DeviceType,
                Provider = primary.Provider ?? primary.DeviceType,
                VendorId = primary.VendorId,
                ProductId = primary.ProductId,
                IsConnected = false,
                LastConnectionTime = now,
                LastSignatureTime = now,
                CertificateStatus = "UNKNOWN",
                MemoryStatus = "UNKNOWN",
                CanCreateInvoices = false,
                FinanzOnlineUsername = primary.FinanzOnlineUsername,
                FinanzOnlineEnabled = false,
                LastFinanzOnlineSync = now,
                KassenId = primary.KassenId,
                CashRegisterId = primary.CashRegisterId,
                IsActive = true,
                CreatedAt = now,
                IsPrimary = false,
                IsBackup = true,
                PrimaryDeviceId = primary.Id,
                HealthStatus = TseHealthStatus.Degraded,
                HealthScore = 60,
                HealthMessage = "Soft auto-scaled backup stub (Development). Not a live cloud TSS.",
                LastHealthCheck = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> SoftScaleDownAsync(
        Guid tenantId,
        int from,
        int to,
        CancellationToken cancellationToken)
    {
        var remove = from - to;
        if (remove <= 0)
            return false;

        var idleBackups = await _db.TseDevices
            .Where(d => d.TenantId == tenantId
                        && d.IsActive
                        && d.IsBackup
                        && !d.IsFailoverActive
                        && !d.IsPrimary)
            .OrderByDescending(d => d.CreatedAt)
            .Take(remove)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (idleBackups.Count == 0)
            return false;

        var now = DateTime.UtcNow;
        foreach (var device in idleBackups)
        {
            device.IsActive = false;
            device.UpdatedAt = now;
            device.HealthMessage = "Deactivated by soft auto-scale (Development).";
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> IsInCooldownAsync(
        Guid tenantId,
        int cooldownMinutes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var lastApplied = await _db.TseScalingHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Applied)
            .OrderByDescending(h => h.EvaluatedAt)
            .Select(h => (DateTime?)h.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lastApplied is null)
            return false;

        return now - lastApplied.Value < TimeSpan.FromMinutes(cooldownMinutes);
    }

    private async Task<TseScalingResultDto> PersistResultAsync(
        Guid tenantId,
        string? tenantName,
        TseScalingPolicy policy,
        int current,
        int recommended,
        double load,
        string action,
        bool applied,
        bool simulationOnly,
        string reason,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var entry = new TseScalingHistoryEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EvaluatedAt = DateTime.UtcNow,
            Action = action,
            FromDevices = current,
            ToDevices = recommended,
            LoadPercent = load,
            Applied = applied,
            SimulationOnly = simulationOnly,
            Reason = Truncate(reason, 1000) ?? reason,
            ActorUserId = Truncate(actorUserId, 450),
        };
        _db.TseScalingHistory.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseScalingResultDto
        {
            TenantId = tenantId,
            TenantName = tenantName,
            EvaluatedAt = entry.EvaluatedAt,
            Action = action,
            CurrentDevices = current,
            RecommendedDevices = recommended,
            CurrentLoadPercent = load,
            Applied = applied,
            SimulationOnly = simulationOnly,
            Reason = entry.Reason,
            Policy = MapPolicy(policy),
        };
    }

    private async Task<TseScalingPolicy> GetOrCreatePolicyAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var policy = await _db.TseScalingPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (policy is not null)
            return policy;

        var opts = _tseOptions.CurrentValue;
        policy = new TseScalingPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Enabled = false,
            MinDevices = 1,
            MaxDevices = 5,
            TargetTransactionsPerDevice = Math.Max(100, opts.CapacityPerDevicePerDay),
            ScaleUpThreshold = Clamp(opts.CapacityWarningUtilizationPercent, 50, 99),
            ScaleDownThreshold = 30,
            CooldownMinutes = 30,
            AutoProvision = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = Truncate(actorUserId, 450),
        };
        _db.TseScalingPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return policy;
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

    private static TseScalingPolicyDto MapPolicy(TseScalingPolicy p) =>
        new()
        {
            TenantId = p.TenantId,
            Enabled = p.Enabled,
            MinDevices = p.MinDevices,
            MaxDevices = p.MaxDevices,
            TargetTransactionsPerDevice = p.TargetTransactionsPerDevice,
            ScaleUpThreshold = p.ScaleUpThreshold,
            ScaleDownThreshold = p.ScaleDownThreshold,
            CooldownMinutes = p.CooldownMinutes,
            AutoProvision = p.AutoProvision,
            UpdatedAt = p.UpdatedAt,
        };

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
