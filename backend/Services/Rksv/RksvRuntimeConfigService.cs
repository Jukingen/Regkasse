using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Rksv;

/// <summary>
/// Singleton overlay for RKSV:Mode / TseMode / FinanzOnlineMode / ShowDemoLabel / BypassTseInDevelopment.
/// Seeded from appsettings on first access; later reads/writes use PostgreSQL (survives restarts).
/// </summary>
public sealed class RksvRuntimeConfigService : IRksvRuntimeConfigService, IDisposable
{
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<RksvRuntimeConfigService> _logger;
    private readonly object _gate = new();
    private readonly Timer _refreshTimer;

    private RksvRuntimeSnapshot? _cache;
    private DateTime _cacheValidUntilUtc;
    private int _disposed;

    public RksvRuntimeConfigService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<RksvRuntimeConfigService> logger)
    {
        _scopeFactory = scopeFactory;
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _tseOptions = tseOptions;
        _logger = logger;
        _refreshTimer = new Timer(
            _ => _ = OnTimerRefreshAsync(),
            null,
            CacheTtl,
            CacheTtl);
    }

    /// <summary>
    /// Non-blocking snapshot for fiscal hot paths. Until the cache is warmed (timer / first admin GET),
    /// falls back to appsettings — the same values used to seed the singleton row.
    /// </summary>
    public RksvRuntimeSnapshot GetEffective()
    {
        lock (_gate)
        {
            if (_cache != null)
                return _cache;
        }

        return FromAppsettingsFallback();
    }

    public async Task<RksvRuntimeSnapshot> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cache != null && DateTime.UtcNow < _cacheValidUntilUtc)
                return _cache;
        }

        await RefreshFromDatabaseAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
            return _cache ?? FromAppsettingsFallback();
    }

    public async Task<RksvRuntimeSnapshot> UpdateAsync(
        RksvRuntimeConfig values,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!RksvRuntimeConfig.IsValidMode(values.Mode))
            throw new RksvRuntimeConfigValidationException("Mode must be Demo or Production.");
        if (!RksvRuntimeConfig.IsValidIntegrationMode(values.TseMode))
            throw new RksvRuntimeConfigValidationException("TseMode must be Simulation or Real.");
        if (!RksvRuntimeConfig.IsValidIntegrationMode(values.FinanzOnlineMode))
            throw new RksvRuntimeConfigValidationException("FinanzOnlineMode must be Simulation or Real.");

        var mode = RksvRuntimeConfig.NormalizeMode(values.Mode);
        var tseMode = RksvRuntimeConfig.NormalizeIntegrationMode(values.TseMode);
        var fonMode = RksvRuntimeConfig.NormalizeIntegrationMode(values.FinanzOnlineMode);
        var showDemoLabel = values.ShowDemoLabel;

        var proposed = new RksvRuntimeSnapshot(
            mode,
            tseMode,
            fonMode,
            showDemoLabel,
            OverlayPersisted: true,
            Source: RksvRuntimeSnapshot.SourceDatabase,
            UpdatedAtUtc: DateTime.UtcNow,
            UpdatedByUserId: updatedByUserId,
            BypassTseInDevelopment: values.BypassTseInDevelopment);

        AssertProductionLockAllows(proposed);

        await WithDbContextAsync(async (db, ct) =>
        {
            await RksvRuntimeConfigEnsure
                .EnsureSingletonAsync(db, _configuration, _hostEnvironment, ct)
                .ConfigureAwait(false);
            var row = await db.RksvRuntimeConfigs
                .FirstAsync(x => x.Id == RksvRuntimeConfig.SingletonId, ct)
                .ConfigureAwait(false);

            row.Mode = mode;
            row.TseMode = tseMode;
            row.FinanzOnlineMode = fonMode;
            row.ShowDemoLabel = showDemoLabel;
            row.BypassTseInDevelopment = values.BypassTseInDevelopment;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = updatedByUserId;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            ReplaceCache(ToSnapshot(row, persisted: true));
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "RKSV runtime config updated. Mode={Mode} TseMode={TseMode} FinanzOnlineMode={FonMode} ShowDemoLabel={ShowDemoLabel} BypassTseInDevelopment={BypassTse} Actor={Actor}",
            mode,
            tseMode,
            fonMode,
            showDemoLabel,
            values.BypassTseInDevelopment,
            updatedByUserId);

        return GetEffective();
    }

    public async Task ReloadCacheAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _cache = null;
            _cacheValidUntilUtc = default;
        }

        await RefreshFromDatabaseAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _refreshTimer.Dispose();
    }

    internal void AssertProductionLockAllows(RksvRuntimeSnapshot proposed)
    {
        var overlay = new TseFiscalConfigLockEvaluator.RksvLockOverlay(
            proposed.Mode,
            proposed.TseMode,
            proposed.IsFinanzOnlineSimulation);
        var eval = TseFiscalConfigLockEvaluator.Evaluate(
            _hostEnvironment,
            _configuration,
            _tseOptions.CurrentValue,
            overlay);
        if (!eval.Ok)
            throw new RksvRuntimeConfigLockException(eval.Reasons);
    }

    private async Task OnTimerRefreshAsync()
    {
        try
        {
            await RefreshFromDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RksvRuntimeConfigService: scheduled cache refresh failed.");
        }
    }

    private async Task WithDbContextAsync(
        Func<AppDbContext, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db, cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshFromDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WithDbContextAsync(async (db, ct) =>
            {
                await RksvRuntimeConfigEnsure
                    .EnsureSingletonAsync(db, _configuration, _hostEnvironment, ct)
                    .ConfigureAwait(false);
                var row = await db.RksvRuntimeConfigs.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == RksvRuntimeConfig.SingletonId, ct)
                    .ConfigureAwait(false);

                ReplaceCache(row == null
                    ? FromAppsettingsFallback()
                    : ToSnapshot(row, persisted: true));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RksvRuntimeConfigService: database refresh failed; using appsettings fallback.");
            ReplaceCache(FromAppsettingsFallback());
        }
    }

    private void ReplaceCache(RksvRuntimeSnapshot snapshot)
    {
        lock (_gate)
        {
            _cache = snapshot;
            _cacheValidUntilUtc = DateTime.UtcNow + CacheTtl;
        }
    }

    private RksvRuntimeSnapshot FromAppsettingsFallback()
    {
        var seed = RksvRuntimeConfigEnsure.SeedFromConfiguration(_configuration, _hostEnvironment);
        return ToSnapshot(seed, persisted: false);
    }

    private static RksvRuntimeSnapshot ToSnapshot(RksvRuntimeConfig row, bool persisted) =>
        new(
            RksvRuntimeConfig.NormalizeMode(row.Mode),
            RksvRuntimeConfig.NormalizeIntegrationMode(row.TseMode),
            RksvRuntimeConfig.NormalizeIntegrationMode(row.FinanzOnlineMode),
            row.ShowDemoLabel,
            OverlayPersisted: persisted,
            Source: persisted ? RksvRuntimeSnapshot.SourceDatabase : RksvRuntimeSnapshot.SourceAppsettings,
            UpdatedAtUtc: row.UpdatedAtUtc,
            UpdatedByUserId: row.UpdatedByUserId,
            BypassTseInDevelopment: row.BypassTseInDevelopment);
}
