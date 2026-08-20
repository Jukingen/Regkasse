using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public interface IFinanzOnlineOutboxSettingsService
{
    FinanzOnlineOutboxWorkerSettingsDto GetSettings(bool canManage);

    Task<FinanzOnlineOutboxWorkerSettingsDto> UpdateAsync(
        UpdateFinanzOnlineOutboxWorkerRequest request,
        string actorUserId,
        bool canManage,
        CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineOutboxSettingsService : IFinanzOnlineOutboxSettingsService
{
    public const string ProductionDisableConfirmRequiredCode = "FO_OUTBOX_PRODUCTION_DISABLE_CONFIRM_REQUIRED";

    private readonly IOptionsMonitor<FinanzOnlineOutboxOptions> _options;
    private readonly FinanzOnlineOutboxEnabledOverrideCache _cache;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FinanzOnlineOutboxSettingsService> _logger;

    public FinanzOnlineOutboxSettingsService(
        IOptionsMonitor<FinanzOnlineOutboxOptions> options,
        FinanzOnlineOutboxEnabledOverrideCache cache,
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditLogService auditLog,
        IHostEnvironment hostEnvironment,
        ILogger<FinanzOnlineOutboxSettingsService> logger)
    {
        _options = options;
        _cache = cache;
        _dbFactory = dbFactory;
        _auditLog = auditLog;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public FinanzOnlineOutboxWorkerSettingsDto GetSettings(bool canManage) =>
        Map(_cache.GetOverlay(), canManage);

    public async Task<FinanzOnlineOutboxWorkerSettingsDto> UpdateAsync(
        UpdateFinanzOnlineOutboxWorkerRequest request,
        string actorUserId,
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        var isProduction = _hostEnvironment.IsProduction();
        var oldOverlay = _cache.GetOverlay();
        var oldEffective = _options.CurrentValue.WithOverlay(oldOverlay);

        if (request.ClearOverride)
        {
            await PersistAsync(overlay: null, actorUserId, cancellationToken).ConfigureAwait(false);
            _cache.ClearOverride();
            await AuditAsync(actorUserId, oldEffective, _options.CurrentValue, "config", cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "FinanzOnline outbox worker overlay cleared by {Actor}; effective Enabled={Enabled} (config)",
                actorUserId,
                _options.CurrentValue.Enabled);
            return Map(_cache.GetOverlay(), canManage);
        }

        ValidateRequest(request);

        if (!RequestHasChange(request))
            return Map(oldOverlay, canManage);

        var wasEnabled = oldEffective.Enabled;
        var nextEnabled = request.Enabled ?? wasEnabled;
        if (isProduction && wasEnabled && !nextEnabled && !request.ConfirmProductionDisable)
        {
            throw new InvalidOperationException(ProductionDisableConfirmRequiredCode);
        }

        var snapshot = SnapshotComplete(oldEffective, request);
        ValidateOverlay(snapshot);
        await PersistAsync(snapshot, actorUserId, cancellationToken).ConfigureAwait(false);
        _cache.SetOverlay(snapshot);
        var nextEffective = _options.CurrentValue.WithOverlay(snapshot);
        await AuditAsync(actorUserId, oldEffective, nextEffective, "global_override", cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "FinanzOnline outbox worker overlay snapshot updated by {Actor}; Enabled={Enabled} PollIntervalSeconds={PollIntervalSeconds} MaxAttempts={MaxAttempts}",
            actorUserId,
            nextEffective.Enabled,
            (int)nextEffective.PollInterval.TotalSeconds,
            nextEffective.MaxAttempts);
        return Map(_cache.GetOverlay(), canManage);
    }

    private static bool RequestHasChange(UpdateFinanzOnlineOutboxWorkerRequest request) =>
        request.Enabled is not null
        || request.PollIntervalSeconds is not null
        || request.MaxAttempts is not null
        || request.BaseDelaySeconds is not null
        || request.BackoffCapSeconds is not null
        || request.JitterMaxSeconds is not null
        || request.ProcessingTimeoutSeconds is not null;

    private static void ValidateRequest(UpdateFinanzOnlineOutboxWorkerRequest request)
    {
        if (request.PollIntervalSeconds is int poll)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.PollIntervalSeconds), poll, FinanzOnlineOutboxWorkerLimits.PollIntervalSeconds);
        if (request.MaxAttempts is int maxAttempts)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.MaxAttempts), maxAttempts, FinanzOnlineOutboxWorkerLimits.MaxAttempts);
        if (request.BaseDelaySeconds is int baseDelay)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.BaseDelaySeconds), baseDelay, FinanzOnlineOutboxWorkerLimits.BaseDelaySeconds);
        if (request.BackoffCapSeconds is int backoff)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.BackoffCapSeconds), backoff, FinanzOnlineOutboxWorkerLimits.BackoffCapSeconds);
        if (request.JitterMaxSeconds is int jitter)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.JitterMaxSeconds), jitter, FinanzOnlineOutboxWorkerLimits.JitterMaxSeconds);
        if (request.ProcessingTimeoutSeconds is int timeout)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.ProcessingTimeoutSeconds), timeout, FinanzOnlineOutboxWorkerLimits.ProcessingTimeoutSeconds);
    }

    private static void ValidateOverlay(FinanzOnlineOutboxOverlay overlay)
    {
        if (overlay.PollIntervalSeconds is int poll)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.PollIntervalSeconds), poll, FinanzOnlineOutboxWorkerLimits.PollIntervalSeconds);
        if (overlay.MaxAttempts is int maxAttempts)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.MaxAttempts), maxAttempts, FinanzOnlineOutboxWorkerLimits.MaxAttempts);
        if (overlay.BaseDelaySeconds is int baseDelay)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.BaseDelaySeconds), baseDelay, FinanzOnlineOutboxWorkerLimits.BaseDelaySeconds);
        if (overlay.BackoffCapSeconds is int backoff)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.BackoffCapSeconds), backoff, FinanzOnlineOutboxWorkerLimits.BackoffCapSeconds);
        if (overlay.JitterMaxSeconds is int jitter)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.JitterMaxSeconds), jitter, FinanzOnlineOutboxWorkerLimits.JitterMaxSeconds);
        if (overlay.ProcessingTimeoutSeconds is int timeout)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.ProcessingTimeoutSeconds), timeout, FinanzOnlineOutboxWorkerLimits.ProcessingTimeoutSeconds);
    }

    private static FinanzOnlineOutboxOverlay SnapshotComplete(
        FinanzOnlineOutboxOptions effective,
        UpdateFinanzOnlineOutboxWorkerRequest request) => new()
    {
        Enabled = request.Enabled ?? effective.Enabled,
        PollIntervalSeconds = request.PollIntervalSeconds ?? (int)Math.Max(1, effective.PollInterval.TotalSeconds),
        MaxAttempts = request.MaxAttempts ?? effective.MaxAttempts,
        BaseDelaySeconds = request.BaseDelaySeconds ?? effective.BaseDelaySeconds,
        BackoffCapSeconds = request.BackoffCapSeconds ?? effective.BackoffCapSeconds,
        JitterMaxSeconds = request.JitterMaxSeconds ?? effective.JitterMaxSeconds,
        ProcessingTimeoutSeconds = request.ProcessingTimeoutSeconds ?? effective.ProcessingTimeoutSeconds,
    };

    private FinanzOnlineOutboxWorkerSettingsDto Map(FinanzOnlineOutboxOverlay? overlay, bool canManage)
    {
        var config = _options.CurrentValue;
        var effective = config.WithOverlay(overlay);
        return new FinanzOnlineOutboxWorkerSettingsDto
        {
            Enabled = effective.Enabled,
            ConfigEnabled = config.Enabled,
            OverrideEnabled = overlay?.Enabled,
            PollIntervalSeconds = Numeric((int)effective.PollInterval.TotalSeconds, (int)config.PollInterval.TotalSeconds, overlay?.PollIntervalSeconds),
            MaxAttempts = Numeric(effective.MaxAttempts, config.MaxAttempts, overlay?.MaxAttempts),
            BaseDelaySeconds = Numeric(effective.BaseDelaySeconds, config.BaseDelaySeconds, overlay?.BaseDelaySeconds),
            BackoffCapSeconds = Numeric(effective.BackoffCapSeconds, config.BackoffCapSeconds, overlay?.BackoffCapSeconds),
            JitterMaxSeconds = Numeric(effective.JitterMaxSeconds, config.JitterMaxSeconds, overlay?.JitterMaxSeconds),
            ProcessingTimeoutSeconds = Numeric(effective.ProcessingTimeoutSeconds, config.ProcessingTimeoutSeconds, overlay?.ProcessingTimeoutSeconds),
            Allowed = MapAllowed(),
            Source = overlay?.HasAny == true ? "global_override" : "config",
            CanManage = canManage,
            IsProduction = _hostEnvironment.IsProduction(),
        };
    }

    private static FinanzOnlineOutboxWorkerNumericDto Numeric(int effective, int config, int? overlay) => new()
    {
        Effective = effective,
        Config = config,
        Overlay = overlay,
    };

    private static FinanzOnlineOutboxWorkerAllowedDto MapAllowed() => new()
    {
        PollIntervalSeconds = Range(FinanzOnlineOutboxWorkerLimits.PollIntervalSeconds),
        MaxAttempts = Range(FinanzOnlineOutboxWorkerLimits.MaxAttempts),
        BaseDelaySeconds = Range(FinanzOnlineOutboxWorkerLimits.BaseDelaySeconds),
        BackoffCapSeconds = Range(FinanzOnlineOutboxWorkerLimits.BackoffCapSeconds),
        JitterMaxSeconds = Range(FinanzOnlineOutboxWorkerLimits.JitterMaxSeconds),
        ProcessingTimeoutSeconds = Range(FinanzOnlineOutboxWorkerLimits.ProcessingTimeoutSeconds),
    };

    private static FinanzOnlineOutboxWorkerRangeDto Range(FinanzOnlineOutboxWorkerRange range) => new()
    {
        Min = range.Min,
        Max = range.Max,
        Values = range.Values,
    };

    private async Task PersistAsync(FinanzOnlineOutboxOverlay? overlay, string actorUserId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.TenantSettings
            .Where(s => s.TenantId == null && s.Key == FinanzOnlineOutboxEnabledOverrideCache.SettingsKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (overlay is null || !overlay.HasAny)
        {
            if (rows.Count > 0)
                db.TenantSettings.RemoveRange(rows);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var value = FinanzOnlineOutboxEnabledOverrideCache.Serialize(overlay);
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Key = FinanzOnlineOutboxEnabledOverrideCache.SettingsKey,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = actorUserId,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = actorUserId;
            foreach (var extra in rows.Skip(1))
                db.TenantSettings.Remove(extra);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task AuditAsync(
        string actorUserId,
        FinanzOnlineOutboxOptions oldEffective,
        FinanzOnlineOutboxOptions newEffective,
        string source,
        CancellationToken cancellationToken) =>
        _auditLog.LogSystemOperationAsync(
            action: "FINANZONLINE_OUTBOX_SETTINGS_SET",
            entityType: "TenantSetting",
            userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
            userRole: "SuperAdmin",
            description: $"FinanzOnline outbox worker settings updated (Enabled {oldEffective.Enabled}->{newEffective.Enabled})",
            status: AuditLogStatus.Success,
            actionType: AuditEventType.FinanzOnlineOutboxSettingsChanged,
            oldValues: Snapshot(oldEffective),
            newValues: new { Enabled = newEffective.Enabled, Source = source, Snapshot = Snapshot(newEffective) });

    private static object Snapshot(FinanzOnlineOutboxOptions opts) => new
    {
        opts.Enabled,
        PollIntervalSeconds = (int)opts.PollInterval.TotalSeconds,
        opts.MaxAttempts,
        opts.BaseDelaySeconds,
        opts.BackoffCapSeconds,
        opts.JitterMaxSeconds,
        opts.ProcessingTimeoutSeconds,
    };
}
