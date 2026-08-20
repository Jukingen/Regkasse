using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public interface IFinanzOnlineRuntimeSettingsService
{
    FinanzOnlineRuntimeSettingsDto GetSettings(bool canManage);

    Task<FinanzOnlineRuntimeSettingsDto> UpdateAsync(
        UpdateFinanzOnlineRuntimeRequest request,
        string actorUserId,
        bool canManage,
        CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineRuntimeSettingsService : IFinanzOnlineRuntimeSettingsService
{
    public const string ProductionSimulationForbiddenCode = "FO_RUNTIME_PRODUCTION_SIMULATION_FORBIDDEN";
    public const string ProductionRealTestForbiddenCode = "FO_RUNTIME_PRODUCTION_REAL_TEST_FORBIDDEN";

    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _session;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassen;
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _transmission;
    private readonly IOptionsMonitor<FinanzOnlineRetryJobOptions> _retry;
    private readonly FinanzOnlineRuntimeOverrideCache _cache;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FinanzOnlineRuntimeSettingsService> _logger;

    public FinanzOnlineRuntimeSettingsService(
        IOptionsMonitor<FinanzOnlineSessionOptions> session,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassen,
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> transmission,
        IOptionsMonitor<FinanzOnlineRetryJobOptions> retry,
        FinanzOnlineRuntimeOverrideCache cache,
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditLogService auditLog,
        IHostEnvironment hostEnvironment,
        ILogger<FinanzOnlineRuntimeSettingsService> logger)
    {
        _session = session;
        _registrierkassen = registrierkassen;
        _transmission = transmission;
        _retry = retry;
        _cache = cache;
        _dbFactory = dbFactory;
        _auditLog = auditLog;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public FinanzOnlineRuntimeSettingsDto GetSettings(bool canManage) =>
        Map(_cache.GetOverlay(), canManage);

    public async Task<FinanzOnlineRuntimeSettingsDto> UpdateAsync(
        UpdateFinanzOnlineRuntimeRequest request,
        string actorUserId,
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        var oldOverlay = _cache.GetOverlay();
        var oldEffective = Map(oldOverlay, canManage);
        var isProduction = _hostEnvironment.IsProduction();

        if (request.ClearOverride)
        {
            await PersistAsync(null, actorUserId, cancellationToken).ConfigureAwait(false);
            _cache.ClearOverride();
            await AuditAsync(actorUserId, oldEffective, Map(null, canManage), "config").ConfigureAwait(false);
            return Map(_cache.GetOverlay(), canManage);
        }

        if (!RequestHasChange(request))
            return oldEffective;

        ValidateRequest(request);

        var nextSim = request.UseSimulation ?? oldEffective.UseSimulation;
        var nextRealSubmit = request.EnableRealTestSubmission ?? oldEffective.EnableRealTestSubmission;
        var nextRealQuery = request.EnableRealTestQuery ?? oldEffective.EnableRealTestQuery;
        if (nextSim)
        {
            nextRealSubmit = false;
            nextRealQuery = false;
        }

        if (isProduction && nextSim)
            throw new InvalidOperationException(ProductionSimulationForbiddenCode);
        if (isProduction && (nextRealSubmit || nextRealQuery))
            throw new InvalidOperationException(ProductionRealTestForbiddenCode);

        var snapshot = new FinanzOnlineRuntimeOverlay
        {
            UseSimulation = nextSim,
            EnableRealTestSubmission = nextRealSubmit,
            EnableRealTestQuery = nextRealQuery,
            RetryJobEnabled = request.RetryJobEnabled ?? oldEffective.RetryJobEnabled,
            RetryIntervalSeconds = request.RetryIntervalSeconds ?? oldEffective.RetryIntervalSeconds.Effective,
            RetryMaxRetryCount = request.RetryMaxRetryCount ?? oldEffective.RetryMaxRetryCount.Effective,
            RetryBaseDelaySeconds = request.RetryBaseDelaySeconds ?? oldEffective.RetryBaseDelaySeconds.Effective,
            RetryBackoffCapSeconds = request.RetryBackoffCapSeconds ?? oldEffective.RetryBackoffCapSeconds.Effective,
            RetryBatchSize = request.RetryBatchSize ?? oldEffective.RetryBatchSize.Effective,
        };
        ValidateOverlay(snapshot);

        await PersistAsync(snapshot, actorUserId, cancellationToken).ConfigureAwait(false);
        _cache.SetOverlay(snapshot);
        var next = Map(_cache.GetOverlay(), canManage);
        await AuditAsync(actorUserId, oldEffective, next, "global_override").ConfigureAwait(false);
        _logger.LogInformation(
            "FinanzOnline runtime overlay snapshot updated by {Actor}; UseSimulation={UseSimulation} RealTestSubmit={RealTestSubmit} RetryJobEnabled={RetryJobEnabled}",
            actorUserId,
            next.UseSimulation,
            next.EnableRealTestSubmission,
            next.RetryJobEnabled);
        return next;
    }

    private static bool RequestHasChange(UpdateFinanzOnlineRuntimeRequest request) =>
        request.UseSimulation is not null
        || request.EnableRealTestSubmission is not null
        || request.EnableRealTestQuery is not null
        || request.RetryJobEnabled is not null
        || request.RetryIntervalSeconds is not null
        || request.RetryMaxRetryCount is not null
        || request.RetryBaseDelaySeconds is not null
        || request.RetryBackoffCapSeconds is not null
        || request.RetryBatchSize is not null;

    private static void ValidateRequest(UpdateFinanzOnlineRuntimeRequest request)
    {
        if (request.RetryIntervalSeconds is int interval)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.RetryIntervalSeconds), interval, FinanzOnlineRuntimeLimits.RetryIntervalSeconds);
        if (request.RetryMaxRetryCount is int maxRetry)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.RetryMaxRetryCount), maxRetry, FinanzOnlineRuntimeLimits.RetryMaxRetryCount);
        if (request.RetryBaseDelaySeconds is int baseDelay)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.RetryBaseDelaySeconds), baseDelay, FinanzOnlineRuntimeLimits.RetryBaseDelaySeconds);
        if (request.RetryBackoffCapSeconds is int cap)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.RetryBackoffCapSeconds), cap, FinanzOnlineRuntimeLimits.RetryBackoffCapSeconds);
        if (request.RetryBatchSize is int batch)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(request.RetryBatchSize), batch, FinanzOnlineRuntimeLimits.RetryBatchSize);
    }

    private static void ValidateOverlay(FinanzOnlineRuntimeOverlay overlay)
    {
        if (overlay.RetryIntervalSeconds is int interval)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.RetryIntervalSeconds), interval, FinanzOnlineRuntimeLimits.RetryIntervalSeconds);
        if (overlay.RetryMaxRetryCount is int maxRetry)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.RetryMaxRetryCount), maxRetry, FinanzOnlineRuntimeLimits.RetryMaxRetryCount);
        if (overlay.RetryBaseDelaySeconds is int baseDelay)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.RetryBaseDelaySeconds), baseDelay, FinanzOnlineRuntimeLimits.RetryBaseDelaySeconds);
        if (overlay.RetryBackoffCapSeconds is int cap)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.RetryBackoffCapSeconds), cap, FinanzOnlineRuntimeLimits.RetryBackoffCapSeconds);
        if (overlay.RetryBatchSize is int batch)
            FinanzOnlineOutboxWorkerLimits.EnsureInRange(nameof(overlay.RetryBatchSize), batch, FinanzOnlineRuntimeLimits.RetryBatchSize);
    }

    private FinanzOnlineRuntimeSettingsDto Map(FinanzOnlineRuntimeOverlay? overlay, bool canManage)
    {
        var isProduction = _hostEnvironment.IsProduction();
        var session = _session.CurrentValue.WithRuntime(overlay, isProduction);
        var rk = _registrierkassen.CurrentValue.WithRuntime(overlay, isProduction);
        var tx = _transmission.CurrentValue.WithRuntime(overlay, isProduction);
        var retry = _retry.CurrentValue.WithRuntime(overlay);
        return new FinanzOnlineRuntimeSettingsDto
        {
            UseSimulation = session.UseSimulation,
            ConfigUseSimulation = _session.CurrentValue.UseSimulation,
            EnableRealTestSubmission = rk.EnableRealTestSubmission,
            ConfigEnableRealTestSubmission = _registrierkassen.CurrentValue.EnableRealTestSubmission,
            EnableRealTestQuery = tx.EnableRealTestQuery,
            ConfigEnableRealTestQuery = _transmission.CurrentValue.EnableRealTestQuery,
            RetryJobEnabled = retry.Enabled,
            ConfigRetryJobEnabled = _retry.CurrentValue.Enabled,
            RetryIntervalSeconds = Numeric((int)retry.Interval.TotalSeconds, (int)_retry.CurrentValue.Interval.TotalSeconds, overlay?.RetryIntervalSeconds),
            RetryMaxRetryCount = Numeric(retry.MaxRetryCount, _retry.CurrentValue.MaxRetryCount, overlay?.RetryMaxRetryCount),
            RetryBaseDelaySeconds = Numeric(retry.BaseDelaySeconds, _retry.CurrentValue.BaseDelaySeconds, overlay?.RetryBaseDelaySeconds),
            RetryBackoffCapSeconds = Numeric(retry.BackoffCapSeconds, _retry.CurrentValue.BackoffCapSeconds, overlay?.RetryBackoffCapSeconds),
            RetryBatchSize = Numeric(retry.BatchSize, _retry.CurrentValue.BatchSize, overlay?.RetryBatchSize),
            Allowed = new FinanzOnlineRuntimeAllowedDto
            {
                RetryIntervalSeconds = Range(FinanzOnlineRuntimeLimits.RetryIntervalSeconds),
                RetryMaxRetryCount = Range(FinanzOnlineRuntimeLimits.RetryMaxRetryCount),
                RetryBaseDelaySeconds = Range(FinanzOnlineRuntimeLimits.RetryBaseDelaySeconds),
                RetryBackoffCapSeconds = Range(FinanzOnlineRuntimeLimits.RetryBackoffCapSeconds),
                RetryBatchSize = Range(FinanzOnlineRuntimeLimits.RetryBatchSize),
            },
            Source = overlay?.HasAny == true ? "global_override" : "config",
            CanManage = canManage,
            IsProduction = isProduction,
        };
    }

    private static FinanzOnlineOutboxWorkerNumericDto Numeric(int effective, int config, int? overlay) => new()
    {
        Effective = effective,
        Config = config,
        Overlay = overlay,
    };

    private static FinanzOnlineOutboxWorkerRangeDto Range(FinanzOnlineOutboxWorkerRange range) => new()
    {
        Min = range.Min,
        Max = range.Max,
        Values = range.Values,
    };

    private async Task PersistAsync(FinanzOnlineRuntimeOverlay? overlay, string actorUserId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.TenantSettings
            .Where(s => s.TenantId == null && s.Key == FinanzOnlineRuntimeOverlay.SettingsKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (overlay is null || !overlay.HasAny)
        {
            if (rows.Count > 0)
                db.TenantSettings.RemoveRange(rows);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var value = FinanzOnlineRuntimeOverrideCache.Serialize(overlay);
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                Key = FinanzOnlineRuntimeOverlay.SettingsKey,
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
        FinanzOnlineRuntimeSettingsDto oldEffective,
        FinanzOnlineRuntimeSettingsDto newEffective,
        string source) =>
        _auditLog.LogSystemOperationAsync(
            action: "FINANZONLINE_RUNTIME_SETTINGS_SET",
            entityType: "TenantSetting",
            userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
            userRole: "SuperAdmin",
            description: $"FinanzOnline runtime settings updated (simulation {oldEffective.UseSimulation}->{newEffective.UseSimulation})",
            status: AuditLogStatus.Success,
            actionType: AuditEventType.FinanzOnlineOutboxSettingsChanged,
            oldValues: new { oldEffective.UseSimulation, oldEffective.EnableRealTestSubmission, oldEffective.RetryJobEnabled },
            newValues: new { newEffective.UseSimulation, newEffective.EnableRealTestSubmission, newEffective.RetryJobEnabled, Source = source });
}
