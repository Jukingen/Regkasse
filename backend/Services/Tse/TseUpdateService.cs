using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Orchestrates rolling catalog/policy updates for TSE tenants without signing downtime.
/// Updates metadata versions only — never flashes fiscal firmware or rewrites receipt chains.
/// </summary>
public sealed class TseUpdateService : ITseUpdateService
{
    private const string BaselineVersion = "0.0.0";
    private const int MaxHistory = 100;

    private static readonly IReadOnlyList<CatalogEntry> Catalog = new[]
    {
        new CatalogEntry(
            TseUpdateTypes.HealthProbePolicy,
            "Health probe policy pack",
            "Rolling refresh of health-check thresholds and sampling guidance for active TSE devices.",
            "2026.7.2",
            TseUpdateRiskLevels.Low,
            RequiresHealthyBackup: false),
        new CatalogEntry(
            TseUpdateTypes.FailoverPolicy,
            "Failover policy pack",
            "Zero-downtime refresh of failover readiness scoring. Requires a healthy backup for Medium risk.",
            "2026.7.1",
            TseUpdateRiskLevels.Medium,
            RequiresHealthyBackup: true),
        new CatalogEntry(
            TseUpdateTypes.CostCatalog,
            "Cost catalog rates",
            "Indicative EUR rate table used by TSE cost analytics (not billing).",
            "2026.7.3",
            TseUpdateRiskLevels.Low,
            RequiresHealthyBackup: false),
        new CatalogEntry(
            TseUpdateTypes.ProviderManifest,
            "Provider capability manifest",
            "Provider feature-flag manifest for Soft/Fake/Cloud TSE adapters used by admin tooling.",
            "2026.7.2",
            TseUpdateRiskLevels.Medium,
            RequiresHealthyBackup: true),
        new CatalogEntry(
            TseUpdateTypes.CertificatePolicy,
            "Certificate warning policy",
            "Updates certificate expiry warning windows used by Super Admin monitoring.",
            "2026.7.1",
            TseUpdateRiskLevels.High,
            RequiresHealthyBackup: true),
    };

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseUpdateService> _logger;

    public TseUpdateService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseUpdateService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<TseUpdateStatusDto> CheckForUpdatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var states = await EnsureStatesAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        var zeroDowntimeCapable = IsZeroDowntimeCapable(devices);

        var available = new List<TseAvailableUpdateDto>();
        foreach (var entry in Catalog)
        {
            var current = states.TryGetValue(entry.UpdateType, out var state)
                ? state.CurrentVersion
                : BaselineVersion;
            if (string.Equals(current, entry.TargetVersion, StringComparison.OrdinalIgnoreCase))
                continue;

            available.Add(new TseAvailableUpdateDto
            {
                UpdateType = entry.UpdateType,
                Name = entry.Name,
                Description = entry.Description,
                CurrentVersion = current,
                TargetVersion = entry.TargetVersion,
                Risk = entry.Risk,
                RequiresHealthyBackup = entry.RequiresHealthyBackup,
                ZeroDowntime = true,
            });
        }

        foreach (var state in states.Values)
        {
            state.LastCheckedAt = now;
            state.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var overallRisk = available.Count == 0
            ? TseUpdateRiskLevels.Low
            : available
                .Select(a => a.Risk)
                .OrderByDescending(RiskRank)
                .First();

        return new TseUpdateStatusDto
        {
            TenantId = tenantId,
            HasUpdates = available.Count > 0,
            AvailableUpdates = available
                .OrderByDescending(a => RiskRank(a.Risk))
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LastChecked = now,
            RiskLevel = overallRisk,
            ActiveDeviceCount = devices.Count,
            ZeroDowntimeCapable = zeroDowntimeCapable,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseUpdateResultDto> ApplyUpdateAsync(
        Guid tenantId,
        string updateType,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(updateType))
            throw new ArgumentException("updateType is required.", nameof(updateType));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var entry = Catalog.FirstOrDefault(c =>
            string.Equals(c.UpdateType, updateType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new ArgumentException($"Unknown update type '{updateType}'.", nameof(updateType));

        var now = DateTime.UtcNow;
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var states = await EnsureStatesAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        var state = states[entry.UpdateType];
        var fromVersion = state.CurrentVersion;

        if (string.Equals(fromVersion, entry.TargetVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new TseUpdateResultDto
            {
                TenantId = tenantId,
                UpdateType = entry.UpdateType,
                Success = true,
                Status = TseUpdateRunStatuses.Succeeded,
                Message = "Already on the target catalog version.",
                FromVersion = fromVersion,
                ToVersion = entry.TargetVersion,
                ZeroDowntime = true,
                DevicesTouched = 0,
                DiagnosticOnly = true,
            };
        }

        var hasHealthyBackup = HasHealthyBackup(devices);
        if (entry.RequiresHealthyBackup && !hasHealthyBackup)
        {
            var blocked = new TseUpdateHistoryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UpdateType = entry.UpdateType,
                Name = entry.Name,
                Description = entry.Description,
                RiskLevel = entry.Risk,
                FromVersion = fromVersion,
                ToVersion = entry.TargetVersion,
                Status = TseUpdateRunStatuses.Blocked,
                ZeroDowntime = true,
                DevicesTouched = 0,
                StartedAt = now,
                CompletedAt = now,
                AppliedBy = TruncateActor(actorUserId),
                Message =
                    "Blocked: a healthy backup TSE is required for zero-downtime rolling apply of this update.",
            };
            _db.TseUpdateHistory.Add(blocked);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new TseUpdateResultDto
            {
                TenantId = tenantId,
                UpdateType = entry.UpdateType,
                Success = false,
                Status = TseUpdateRunStatuses.Blocked,
                Message = blocked.Message!,
                FromVersion = fromVersion,
                ToVersion = entry.TargetVersion,
                ZeroDowntime = true,
                DevicesTouched = 0,
                HistoryId = blocked.Id,
                DiagnosticOnly = true,
            };
        }

        // Rolling apply: touch backups first conceptually, then primaries — metadata only.
        var ordered = devices
            .OrderByDescending(d => d.IsBackup)
            .ThenByDescending(d => d.IsPrimary)
            .ThenBy(d => d.SerialNumber)
            .ToList();

        state.CurrentVersion = entry.TargetVersion;
        state.LastAppliedAt = now;
        state.LastCheckedAt = now;
        state.UpdatedAt = now;

        var history = new TseUpdateHistoryEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UpdateType = entry.UpdateType,
            Name = entry.Name,
            Description = entry.Description,
            RiskLevel = entry.Risk,
            FromVersion = fromVersion,
            ToVersion = entry.TargetVersion,
            Status = TseUpdateRunStatuses.Succeeded,
            ZeroDowntime = true,
            DevicesTouched = ordered.Count,
            StartedAt = now,
            CompletedAt = now,
            AppliedBy = TruncateActor(actorUserId),
            Message =
                $"Applied {entry.Name} {fromVersion} → {entry.TargetVersion} with zero-downtime rolling strategy "
                + $"across {ordered.Count} device(s). Fiscal signing chain unchanged.",
        };
        _db.TseUpdateHistory.Add(history);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "TSE update applied TenantId={TenantId} Type={Type} {From}→{To} Devices={Devices} Actor={Actor}",
            tenantId,
            entry.UpdateType,
            fromVersion,
            entry.TargetVersion,
            ordered.Count,
            actorUserId);

        return new TseUpdateResultDto
        {
            TenantId = tenantId,
            UpdateType = entry.UpdateType,
            Success = true,
            Status = TseUpdateRunStatuses.Succeeded,
            Message = history.Message!,
            FromVersion = fromVersion,
            ToVersion = entry.TargetVersion,
            ZeroDowntime = true,
            DevicesTouched = ordered.Count,
            HistoryId = history.Id,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseUpdateHistoryDto> GetUpdateHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var items = await _db.TseUpdateHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.StartedAt)
            .Take(MaxHistory)
            .Select(h => new TseUpdateHistoryItemDto
            {
                Id = h.Id,
                UpdateType = h.UpdateType,
                Name = h.Name,
                Description = h.Description,
                RiskLevel = h.RiskLevel,
                FromVersion = h.FromVersion,
                ToVersion = h.ToVersion,
                Status = h.Status,
                ZeroDowntime = h.ZeroDowntime,
                DevicesTouched = h.DevicesTouched,
                StartedAt = h.StartedAt,
                CompletedAt = h.CompletedAt,
                Message = h.Message,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TseUpdateHistoryDto
        {
            TenantId = tenantId,
            GeneratedAt = DateTime.UtcNow,
            Items = items,
            DiagnosticOnly = true,
        };
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }

    private async Task<List<TseDevice>> LoadActiveDevicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<Dictionary<string, TseUpdateState>> EnsureStatesAsync(
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await _db.TseUpdateStates
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var map = existing.ToDictionary(s => s.UpdateType, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Catalog)
        {
            if (map.ContainsKey(entry.UpdateType))
                continue;

            var state = new TseUpdateState
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UpdateType = entry.UpdateType,
                CurrentVersion = BaselineVersion,
                UpdatedAt = now,
            };
            _db.TseUpdateStates.Add(state);
            map[entry.UpdateType] = state;
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return map;
    }

    private bool HasHealthyBackup(IReadOnlyList<TseDevice> devices)
    {
        var opts = _tseOptions.CurrentValue;
        return devices.Any(d =>
            d.IsBackup
            && !d.IsFailoverActive
            && d.HealthStatus == TseHealthStatus.Healthy
            && d.HealthScore >= opts.FailoverHealthyMinScore);
    }

    private bool IsZeroDowntimeCapable(IReadOnlyList<TseDevice> devices)
    {
        if (devices.Count == 0)
            return true; // catalog-only updates still apply without device downtime
        var hasPrimary = devices.Any(d => d.IsPrimary || d.IsFailoverActive);
        return !hasPrimary || HasHealthyBackup(devices) || devices.Count == 1;
    }

    private static int RiskRank(string? risk) => risk switch
    {
        TseUpdateRiskLevels.High => 3,
        TseUpdateRiskLevels.Medium => 2,
        TseUpdateRiskLevels.Low => 1,
        _ => 0,
    };

    private static string? TruncateActor(string? actor) =>
        string.IsNullOrWhiteSpace(actor)
            ? null
            : (actor.Length <= 450 ? actor : actor[..450]);

    private sealed record CatalogEntry(
        string UpdateType,
        string Name,
        string Description,
        string TargetVersion,
        string Risk,
        bool RequiresHealthyBackup);
}
