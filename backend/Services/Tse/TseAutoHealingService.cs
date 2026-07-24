using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Diagnoses unhealthy TSE devices and applies safe heal actions (re-probe, clear errors, optional failover).
/// Never mutates fiscal signature material or receipt chains.
/// </summary>
public sealed class TseAutoHealingService : ITseAutoHealingService
{
    private const int MaxHistory = 100;

    private readonly AppDbContext _db;
    private readonly ITseDeviceHealthCheckService _health;
    private readonly ITseFailoverService _failover;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseAutoHealingService> _logger;

    public TseAutoHealingService(
        AppDbContext db,
        ITseDeviceHealthCheckService health,
        ITseFailoverService failover,
        IOptionsMonitor<TseOptions> tseOptions,
        IActivityEventPublisher activity,
        ILogger<TseAutoHealingService> logger)
    {
        _db = db;
        _health = health;
        _failover = failover;
        _tseOptions = tseOptions;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseHealingResultDto> DiagnoseAndHealAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("deviceId is required.", nameof(deviceId));

        var device = await _db.TseDevices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
            throw new KeyNotFoundException($"TSE device {deviceId} was not found.");
        if (device.TenantId is null || device.TenantId == Guid.Empty)
            throw new InvalidOperationException("Device has no tenant context.");

        var tenantId = device.TenantId.Value;
        var config = await EnsureConfigurationAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var scoreBefore = device.HealthScore;
        var now = DateTime.UtcNow;

        var matched = MatchRule(device, config);
        if (matched is null)
        {
            return await PersistResultAsync(
                tenantId,
                deviceId,
                scoreBefore,
                scoreAfter: scoreBefore,
                condition: null,
                action: null,
                status: TseHealingAttemptStatuses.DiagnosedOnly,
                applied: false,
                message: "No healing rule matched; device does not require auto-heal.",
                actorUserId,
                rule: null,
                cancellationToken).ConfigureAwait(false);
        }

        if (!config.Enabled)
        {
            return await PersistResultAsync(
                tenantId,
                deviceId,
                scoreBefore,
                scoreAfter: scoreBefore,
                matched.Condition,
                matched.Action,
                TseHealingAttemptStatuses.DiagnosedOnly,
                applied: false,
                $"Matched {matched.Condition} → {matched.Action}, but auto-healing is disabled.",
                actorUserId,
                matched,
                cancellationToken).ConfigureAwait(false);
        }

        var cooldown = TimeSpan.FromMinutes(Math.Clamp(config.CooldownMinutes, 1, 24 * 60));
        var recentWindow = now - cooldown;
        var recentAttempts = await _db.TseHealingHistory.AsNoTracking()
            .CountAsync(
                h => h.DeviceId == deviceId
                     && h.StartedAt >= recentWindow
                     && h.Applied,
                cancellationToken)
            .ConfigureAwait(false);
        if (recentAttempts >= Math.Max(1, config.MaxAutoHealAttempts))
        {
            return await PersistResultAsync(
                tenantId,
                deviceId,
                scoreBefore,
                scoreAfter: scoreBefore,
                matched.Condition,
                matched.Action,
                TseHealingAttemptStatuses.Cooldown,
                applied: false,
                $"Cooldown active: {recentAttempts} applied attempt(s) in the last {config.CooldownMinutes} minute(s).",
                actorUserId,
                matched,
                cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(matched.Action, TseHealingActions.AttemptFailover, StringComparison.OrdinalIgnoreCase)
            && !config.AllowAutoFailover)
        {
            return await PersistResultAsync(
                tenantId,
                deviceId,
                scoreBefore,
                scoreAfter: scoreBefore,
                matched.Condition,
                matched.Action,
                TseHealingAttemptStatuses.DiagnosedOnly,
                applied: false,
                "Failover heal matched but AllowAutoFailover is false.",
                actorUserId,
                matched,
                cancellationToken).ConfigureAwait(false);
        }

        var (success, applied, status, message, scoreAfter) = await ApplyActionAsync(
                device,
                matched.Action,
                cancellationToken)
            .ConfigureAwait(false);

        matched.LastTriggeredAt = now;
        var result = await PersistResultAsync(
            tenantId,
            deviceId,
            scoreBefore,
            scoreAfter,
            matched.Condition,
            matched.Action,
            status,
            applied,
            message,
            actorUserId,
            matched,
            cancellationToken).ConfigureAwait(false);

        if (config.NotifyOnHeal && applied)
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TseAutoHealExecuted,
                    new
                    {
                        DeviceId = deviceId.ToString("D"),
                        TenantId = tenantId.ToString("D"),
                        Condition = matched.Condition,
                        Action = matched.Action,
                        Status = status,
                        Message = message,
                        HealthScoreBefore = scoreBefore,
                        HealthScoreAfter = scoreAfter,
                    },
                    actorUserId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    dedupKey: result.HistoryId?.ToString("N"),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation(
            "TSE auto-heal DeviceId={DeviceId} Condition={Condition} Action={Action} Status={Status} Applied={Applied}",
            deviceId,
            matched.Condition,
            matched.Action,
            status,
            applied);

        result.Success = success;
        return result;
    }

    public async Task<TseHealingReportDto> GetHealingHistoryAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        take = Math.Clamp(take, 1, MaxHistory);

        var items = await _db.TseHealingHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.StartedAt)
            .Take(take)
            .Select(h => new TseHealingHistoryItemDto
            {
                Id = h.Id,
                DeviceId = h.DeviceId,
                Condition = h.Condition,
                Action = h.Action,
                Status = h.Status,
                Applied = h.Applied,
                HealthScoreBefore = h.HealthScoreBefore,
                HealthScoreAfter = h.HealthScoreAfter,
                Message = h.Message,
                StartedAt = h.StartedAt,
                CompletedAt = h.CompletedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TseHealingReportDto
        {
            TenantId = tenantId,
            GeneratedAt = DateTime.UtcNow,
            TotalAttempts = items.Count,
            AppliedCount = items.Count(i => i.Applied),
            SucceededCount = items.Count(i =>
                string.Equals(i.Status, TseHealingAttemptStatuses.Succeeded, StringComparison.OrdinalIgnoreCase)),
            Items = items,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseHealingConfigurationDto> GetHealingConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var config = await EnsureConfigurationAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return MapConfig(config);
    }

    public async Task<TseHealingConfigurationDto> ConfigureHealingAsync(
        Guid tenantId,
        ConfigureTseHealingRequestDto config,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(config);

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var entity = await EnsureConfigurationAsync(tenantId, cancellationToken).ConfigureAwait(false);

        entity.Enabled = config.Enabled;
        entity.MaxAutoHealAttempts = Math.Clamp(config.MaxAutoHealAttempts, 1, 20);
        entity.CooldownMinutes = Math.Clamp(config.CooldownMinutes, 1, 24 * 60);
        entity.NotifyOnHeal = config.NotifyOnHeal;
        entity.AllowAutoFailover = config.AllowAutoFailover;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = TruncateActor(actorUserId);

        if (config.Rules is { Count: > 0 })
        {
            var oldRules = await _db.TseHealingRules
                .Where(r => r.ConfigurationId == entity.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (oldRules.Count > 0)
                _db.TseHealingRules.RemoveRange(oldRules);

            // Detach any navigation-tracked rules that were never persisted (InMemory edge case).
            foreach (var tracked in entity.Rules.ToList())
            {
                if (oldRules.All(r => r.Id != tracked.Id))
                    _db.Entry(tracked).State = EntityState.Detached;
            }

            entity.Rules.Clear();
            foreach (var ruleDto in config.Rules)
            {
                ValidateRule(ruleDto.Condition, ruleDto.Action, ruleDto.Status);
                var created = new TseHealingRule
                {
                    Id = Guid.NewGuid(),
                    ConfigurationId = entity.Id,
                    Condition = ruleDto.Condition.Trim(),
                    Action = ruleDto.Action.Trim(),
                    Priority = Math.Clamp(ruleDto.Priority, 1, 1000),
                    Status = NormalizeRuleStatus(ruleDto.Status),
                };
                _db.TseHealingRules.Add(created);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        // Re-load to avoid navigation duplicate fix-up after replace.
        var refreshed = await _db.TseHealingConfigurations
            .AsNoTracking()
            .Include(c => c.Rules)
            .FirstAsync(c => c.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);
        return MapConfig(refreshed);
    }

    private async Task<(bool Success, bool Applied, string Status, string Message, int? ScoreAfter)> ApplyActionAsync(
        TseDevice device,
        string action,
        CancellationToken cancellationToken)
    {
        if (string.Equals(action, TseHealingActions.ReprobeHealth, StringComparison.OrdinalIgnoreCase))
        {
            var result = await _health.CheckHealthAsync(device.Id, cancellationToken).ConfigureAwait(false);
            await _db.Entry(device).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return (
                true,
                true,
                TseHealingAttemptStatuses.Succeeded,
                $"Re-probed health → status={result.Status}, score={result.HealthScore}.",
                result.HealthScore);
        }

        if (string.Equals(action, TseHealingActions.ClearTransientError, StringComparison.OrdinalIgnoreCase))
        {
            var probe = await _health.CheckHealthAsync(device.Id, cancellationToken).ConfigureAwait(false);
            await _db.Entry(device).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (probe.HealthScore >= _tseOptions.CurrentValue.FailoverHealthyMinScore
                && !string.IsNullOrWhiteSpace(device.ErrorMessage))
            {
                device.ErrorMessage = null;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (
                    true,
                    true,
                    TseHealingAttemptStatuses.Succeeded,
                    "Cleared transient ErrorMessage after healthy re-probe.",
                    probe.HealthScore);
            }

            return (
                true,
                true,
                TseHealingAttemptStatuses.Succeeded,
                "Re-probed device; no transient error cleared.",
                probe.HealthScore);
        }

        if (string.Equals(action, TseHealingActions.AttemptFailover, StringComparison.OrdinalIgnoreCase))
        {
            var failover = await _failover.CheckAndFailoverAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);
            await _db.Entry(device).ReloadAsync(cancellationToken).ConfigureAwait(false);
            var ok = failover.Succeeded;
            return (
                ok,
                true,
                ok ? TseHealingAttemptStatuses.Succeeded : TseHealingAttemptStatuses.Failed,
                string.IsNullOrWhiteSpace(failover.Message)
                    ? (ok ? "Failover heal executed." : "Failover heal failed.")
                    : failover.Message,
                device.HealthScore);
        }

        return (
            false,
            false,
            TseHealingAttemptStatuses.Failed,
            $"Unknown heal action '{action}'.",
            device.HealthScore);
    }

    private TseHealingRule? MatchRule(TseDevice device, TseHealingConfiguration config)
    {
        var opts = _tseOptions.CurrentValue;
        var enabledRules = config.Rules
            .Where(r => string.Equals(r.Status, TseHealingRuleStatuses.Enabled, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Condition)
            .ToList();

        foreach (var rule in enabledRules)
        {
            if (string.Equals(rule.Condition, TseHealingConditions.DeviceOffline, StringComparison.OrdinalIgnoreCase))
            {
                if (device.HealthStatus == TseHealthStatus.Offline || !device.IsConnected)
                    return rule;
            }
            else if (string.Equals(rule.Condition, TseHealingConditions.HealthBelowDegraded, StringComparison.OrdinalIgnoreCase))
            {
                if (device.HealthScore < opts.FailoverDegradedMinScore
                    || device.HealthStatus is TseHealthStatus.Degraded or TseHealthStatus.Offline)
                    return rule;
            }
            else if (string.Equals(rule.Condition, TseHealingConditions.TransientErrorPresent, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(device.ErrorMessage))
                    return rule;
            }
            else if (string.Equals(rule.Condition, TseHealingConditions.PrimaryUnhealthyWithBackup, StringComparison.OrdinalIgnoreCase))
            {
                if (!device.IsPrimary)
                    continue;
                if (device.HealthScore >= opts.FailoverHealthyMinScore
                    && device.HealthStatus == TseHealthStatus.Healthy)
                    continue;
                return rule;
            }
        }

        return null;
    }

    private async Task<TseHealingResultDto> PersistResultAsync(
        Guid tenantId,
        Guid deviceId,
        int scoreBefore,
        int? scoreAfter,
        string? condition,
        string? action,
        string status,
        bool applied,
        string message,
        string? actorUserId,
        TseHealingRule? rule,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var entry = new TseHealingHistoryEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            Condition = condition,
            Action = action,
            Status = status,
            Applied = applied,
            HealthScoreBefore = scoreBefore,
            HealthScoreAfter = scoreAfter,
            Message = Truncate(message, 2000),
            StartedAt = now,
            CompletedAt = now,
            ActorUserId = TruncateActor(actorUserId),
        };
        _db.TseHealingHistory.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseHealingResultDto
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            Success = status is TseHealingAttemptStatuses.Succeeded or TseHealingAttemptStatuses.DiagnosedOnly,
            Applied = applied,
            Status = status,
            Message = message,
            HealthScoreBefore = scoreBefore,
            HealthScoreAfter = scoreAfter,
            MatchedCondition = condition,
            ActionTaken = action,
            HistoryId = entry.Id,
            DiagnosticOnly = true,
        };
    }

    private async Task<TseHealingConfiguration> EnsureConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var config = await _db.TseHealingConfigurations
            .Include(c => c.Rules)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (config is not null)
            return config;

        config = new TseHealingConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Enabled = false,
            MaxAutoHealAttempts = 3,
            CooldownMinutes = 30,
            NotifyOnHeal = true,
            AllowAutoFailover = false,
            CreatedAt = DateTime.UtcNow,
            Rules =
            {
                new TseHealingRule
                {
                    Id = Guid.NewGuid(),
                    Condition = TseHealingConditions.DeviceOffline,
                    Action = TseHealingActions.ReprobeHealth,
                    Priority = 10,
                    Status = TseHealingRuleStatuses.Enabled,
                },
                new TseHealingRule
                {
                    Id = Guid.NewGuid(),
                    Condition = TseHealingConditions.HealthBelowDegraded,
                    Action = TseHealingActions.ReprobeHealth,
                    Priority = 20,
                    Status = TseHealingRuleStatuses.Enabled,
                },
                new TseHealingRule
                {
                    Id = Guid.NewGuid(),
                    Condition = TseHealingConditions.TransientErrorPresent,
                    Action = TseHealingActions.ClearTransientError,
                    Priority = 30,
                    Status = TseHealingRuleStatuses.Enabled,
                },
                new TseHealingRule
                {
                    Id = Guid.NewGuid(),
                    Condition = TseHealingConditions.PrimaryUnhealthyWithBackup,
                    Action = TseHealingActions.AttemptFailover,
                    Priority = 40,
                    Status = TseHealingRuleStatuses.Enabled,
                },
            },
        };

        // Fix ConfigurationId FK after parent id known
        foreach (var rule in config.Rules)
            rule.ConfigurationId = config.Id;

        _db.TseHealingConfigurations.Add(config);
        foreach (var rule in config.Rules)
            _db.TseHealingRules.Add(rule);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return config;
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }

    private static TseHealingConfigurationDto MapConfig(TseHealingConfiguration config) =>
        new()
        {
            TenantId = config.TenantId,
            Enabled = config.Enabled,
            MaxAutoHealAttempts = config.MaxAutoHealAttempts,
            CooldownMinutes = config.CooldownMinutes,
            NotifyOnHeal = config.NotifyOnHeal,
            AllowAutoFailover = config.AllowAutoFailover,
            Rules = config.Rules
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Condition)
                .Select(r => new TseHealingRuleDto
                {
                    Id = r.Id,
                    Condition = r.Condition,
                    Action = r.Action,
                    Priority = r.Priority,
                    Status = r.Status,
                    LastTriggeredAt = r.LastTriggeredAt,
                })
                .ToList(),
            DiagnosticOnly = true,
        };

    private static void ValidateRule(string condition, string action, string status)
    {
        if (string.IsNullOrWhiteSpace(condition))
            throw new ArgumentException("Rule condition is required.");
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Rule action is required.");
        NormalizeRuleStatus(status);
    }

    private static string NormalizeRuleStatus(string? status) =>
        string.Equals(status, TseHealingRuleStatuses.Disabled, StringComparison.OrdinalIgnoreCase)
            ? TseHealingRuleStatuses.Disabled
            : TseHealingRuleStatuses.Enabled;

    private static string? TruncateActor(string? actor) =>
        string.IsNullOrWhiteSpace(actor) ? null : Truncate(actor, 450);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
