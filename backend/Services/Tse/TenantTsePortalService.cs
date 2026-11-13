using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Tenant-scoped TSE status / health history for Mandanten self-service.
/// Read-only; does not mutate devices or fiscal state.
/// </summary>
public sealed class TenantTsePortalService : ITenantTsePortalService
{
    private const int MaxHistoryDays = 90;
    private const int MaxHistoryPoints = 500;

    private readonly AppDbContext _db;

    public TenantTsePortalService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TenantTseStatusDto> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.SerialNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var deviceDtos = devices.Select(d =>
        {
            int? daysUntil = null;
            if (d.ExpiresAt.HasValue)
            {
                daysUntil = (d.ExpiresAt.Value.ToUniversalTime().Date - now.Date).Days;
            }

            return new TenantTseDeviceStatusDto
            {
                DeviceId = d.Id,
                SerialNumber = d.SerialNumber,
                IsPrimary = d.IsPrimary,
                IsBackup = d.IsBackup,
                HealthStatus = d.HealthStatus.ToString(),
                HealthScore = d.HealthScore,
                ExpiresAt = d.ExpiresAt,
                DaysUntilExpiry = daysUntil,
                LastHealthCheck = d.LastHealthCheck,
            };
        }).ToList();

        var overallScore = deviceDtos.Count == 0
            ? 0
            : (int)Math.Round(deviceDtos.Average(d => d.HealthScore));

        string overallHealth;
        if (deviceDtos.Count == 0)
            overallHealth = "Unknown";
        else if (deviceDtos.All(d =>
                     string.Equals(d.HealthStatus, nameof(TseHealthStatus.Healthy), StringComparison.OrdinalIgnoreCase)))
            overallHealth = "Healthy";
        else
            overallHealth = "Degraded";

        var nearestExpiry = deviceDtos
            .Where(d => d.DaysUntilExpiry.HasValue)
            .Select(d => d.DaysUntilExpiry!.Value)
            .DefaultIfEmpty()
            .Min();

        return new TenantTseStatusDto
        {
            TenantId = tenantId,
            Devices = deviceDtos,
            OverallHealth = overallHealth,
            OverallHealthScore = overallScore,
            NearestDaysUntilExpiry = deviceDtos.Any(d => d.DaysUntilExpiry.HasValue) ? nearestExpiry : null,
            GeneratedAt = now,
        };
    }

    public async Task<TenantTseHealthHistoryDto> GetHealthHistoryAsync(
        Guid tenantId,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        days = Math.Clamp(days, 1, MaxHistoryDays);
        var fromUtc = DateTime.UtcNow.AddDays(-days);

        var deviceIds = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.Id, d.SerialNumber })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var idSet = deviceIds.Select(d => d.Id).ToHashSet();
        var serialById = deviceIds.ToDictionary(d => d.Id, d => d.SerialNumber);

        var samples = await _db.TseDeviceHealthSamples.AsNoTracking()
            .Where(s =>
                s.CheckedAtUtc >= fromUtc
                && (s.TenantId == tenantId || (s.DeviceId != Guid.Empty && idSet.Contains(s.DeviceId))))
            .OrderByDescending(s => s.CheckedAtUtc)
            .Take(MaxHistoryPoints)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var points = samples
            .Select(s => new TenantTseHealthHistoryPointDto
            {
                CheckedAtUtc = s.CheckedAtUtc,
                DeviceId = s.DeviceId,
                SerialNumber = serialById.TryGetValue(s.DeviceId, out var sn) ? sn : null,
                HealthScore = s.HealthScore,
                HealthStatus = s.HealthStatus.ToString(),
                ResponseTimeMs = s.ResponseTimeMs,
            })
            .OrderBy(p => p.CheckedAtUtc)
            .ToList();

        return new TenantTseHealthHistoryDto
        {
            TenantId = tenantId,
            Days = days,
            Points = points,
        };
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }
}
