using System.Threading;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Singleton: loads development-mode settings from PostgreSQL with an in-memory cache refreshed every 30 seconds
/// or immediately after <see cref="UpdateSettingsAsync"/>. Bypass-style flags are effective only when the host environment is Development.
/// </summary>
public sealed class DevelopmentModeService : IDevelopmentModeService, IDisposable
{
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DevelopmentModeService> _logger;
    private readonly object _gate = new();
    private readonly Timer _refreshTimer;

    private DevelopmentModeSettings? _cache;
    private DateTime _cacheValidUntilUtc;
    private int _disposed;

    public DevelopmentModeService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment hostEnvironment,
        ILogger<DevelopmentModeService> logger)
    {
        _scopeFactory = scopeFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;

        _refreshTimer = new Timer(
            _ => _ = OnTimerRefreshAsync(),
            null,
            CacheTtl,
            CacheTtl);
    }

    /// <inheritdoc />
    public async Task<DevelopmentModeSettings> GetSettingsAsync()
    {
        lock (_gate)
        {
            if (_cache != null && DateTime.UtcNow < _cacheValidUntilUtc)
                return Clone(_cache);
        }

        await RefreshFromDatabaseAsync(CancellationToken.None).ConfigureAwait(false);

        lock (_gate)
            return Clone(_cache ?? InactiveDefaults());
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(DevelopmentModeSettings settings, Guid? updatedByUserId)
    {
        if (!_hostEnvironment.IsDevelopment())
            throw new InvalidOperationException("Development mode settings can only be updated when the host environment is Development.");

        await WithDbContextAsync(async (db, cancellationToken) =>
        {
            await DevelopmentModeSettingsEnsure.EnsureSingletonAsync(db, cancellationToken).ConfigureAwait(false);
            var row = await db.DevelopmentModeSettings
                .FirstAsync(x => x.Id == DevelopmentModeSettings.SingletonId, cancellationToken)
                .ConfigureAwait(false);

            row.Enabled = settings.Enabled;
            row.BypassLicense = settings.BypassLicense;
            row.BypassNtpCheck = settings.BypassNtpCheck;
            row.BypassTseCheck = settings.BypassTseCheck;
            row.SimulateOffline = settings.SimulateOffline;
            row.ForceOnline = settings.ForceOnline;
            row.ValidDays = settings.ValidDays < 1 ? 1 : settings.ValidDays;
            row.Features = settings.Features ?? [];
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = updatedByUserId;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            ReplaceCache(Clone(row));
        }, CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReloadSettingsCacheAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _cache = null;
            _cacheValidUntilUtc = default;
        }

        await RefreshFromDatabaseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool IsDevelopmentModeEnabled() =>
        EffectiveDev() && Snapshot.Enabled;

    /// <inheritdoc />
    public bool ShouldBypassLicense() =>
        EffectiveDev() && Snapshot.Enabled && Snapshot.BypassLicense;

    /// <inheritdoc />
    public bool ShouldBypassNtpCheck() =>
        EffectiveDev() && Snapshot.Enabled && Snapshot.BypassNtpCheck;

    /// <inheritdoc />
    public bool ShouldBypassTseCheck() =>
        EffectiveDev() && Snapshot.Enabled && Snapshot.BypassTseCheck;

    /// <inheritdoc />
    public bool ShouldSimulateOffline() =>
        EffectiveDev() && Snapshot.Enabled && Snapshot.SimulateOffline;

    /// <inheritdoc />
    public bool ShouldForceOnline() =>
        EffectiveDev() && Snapshot.Enabled && Snapshot.ForceOnline;

    /// <inheritdoc />
    public int GetValidDays()
    {
        var s = Snapshot;
        var days = s.ValidDays < 1 ? 365 : s.ValidDays;
        return EffectiveDev() && s.Enabled ? days : 365;
    }

    /// <inheritdoc />
    public string[] GetFeatures()
    {
        var s = Snapshot;
        if (!EffectiveDev() || !s.Enabled)
            return [];
        return s.Features is { Length: > 0 } ? (string[])s.Features.Clone() : [];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _refreshTimer.Dispose();
    }

    private async Task OnTimerRefreshAsync()
    {
        try
        {
            await RefreshFromDatabaseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DevelopmentModeService: scheduled cache refresh failed.");
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
        await WithDbContextAsync(async (db, ct) =>
        {
            await DevelopmentModeSettingsEnsure.EnsureSingletonAsync(db, ct).ConfigureAwait(false);
            var row = await db.DevelopmentModeSettings.AsNoTracking()
                .FirstAsync(x => x.Id == DevelopmentModeSettings.SingletonId, ct)
                .ConfigureAwait(false);

            ReplaceCache(Clone(row));
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ReplaceCache(DevelopmentModeSettings snapshot)
    {
        lock (_gate)
        {
            _cache = snapshot;
            _cacheValidUntilUtc = DateTime.UtcNow + CacheTtl;
        }
    }

    private bool EffectiveDev() => _hostEnvironment.IsDevelopment();

    private DevelopmentModeSettings Snapshot
    {
        get
        {
            lock (_gate)
                return _cache ?? InactiveDefaults();
        }
    }

    private static DevelopmentModeSettings InactiveDefaults() =>
        DevelopmentModeSettings.CreateDefaultSingleton();

    private static DevelopmentModeSettings Clone(DevelopmentModeSettings s) =>
        new()
        {
            Id = s.Id,
            Enabled = s.Enabled,
            BypassLicense = s.BypassLicense,
            BypassNtpCheck = s.BypassNtpCheck,
            BypassTseCheck = s.BypassTseCheck,
            SimulateOffline = s.SimulateOffline,
            ForceOnline = s.ForceOnline,
            ValidDays = s.ValidDays,
            Features = s.Features is { Length: > 0 } ? (string[])s.Features.Clone() : [],
            UpdatedAtUtc = s.UpdatedAtUtc,
            UpdatedByUserId = s.UpdatedByUserId,
        };
}
