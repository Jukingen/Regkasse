using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Combines the in-memory TSE health snapshot with tenant SCU / device rows for the POS header.
/// Does not probe Fiskaly on every request — uses cached health + DB.
/// </summary>
public sealed class PosTseStatusService : IPosTseStatusService
{
    private readonly AppDbContext _db;
    private readonly ITseHealthMonitor _health;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;

    public PosTseStatusService(
        AppDbContext db,
        ITseHealthMonitor health,
        IWebHostEnvironment environment,
        IOptionsMonitor<DevelopmentOptions> developmentOptions)
    {
        _db = db;
        _health = health;
        _environment = environment;
        _developmentOptions = developmentOptions;
    }

    public async Task<PosTseStatusDto> GetStatusAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (!OpenApiExportMode.IsEnabled
            && _environment.IsDevelopment()
            && _developmentOptions.CurrentValue.SimulateTseUnavailable)
        {
            return new PosTseStatusDto
            {
                Status = PosTseIndicatorStatuses.Inactive,
                Message = "Development simulation: TSE reported unavailable.",
                LastCheck = DateTime.UtcNow,
                Cached = false,
                OperationalHealth = TseOperationalHealth.Offline.ToString(),
                LastErrorMessageSafe = "Entwicklungssimulation: TSE als nicht verfügbar gemeldet.",
            };
        }

        var snap = _health.Snapshot;
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var registerIds = await _db.CashRegisters
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var device = await _db.TseDevices
            .AsNoTracking()
            .Where(d => d.IsActive
                && (d.TenantId == tenantId || registerIds.Contains(d.KassenId)))
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int? queueCount = null;
        if (cashRegisterId is { } rid && rid != Guid.Empty)
        {
            queueCount = await _db.OfflineTransactions.AsNoTracking()
                .CountAsync(
                    x => x.CashRegisterId == rid && x.Status == OfflineTransactionStatus.NonFiscalPending,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var scuId = ResolveScuId(tenant, device);
        var cached = snap.ConsecutiveFailures > 0 && snap.LastSuccessfulPingUtc is not null;
        var certUntil = device?.ExpiresAt;
        var (status, message, operationalHealth) = Classify(snap, tenant, device, cached);

        return new PosTseStatusDto
        {
            Status = status,
            Message = message,
            LastCheck = snap.LastCheckUtc ?? DateTime.UtcNow,
            ScuId = scuId,
            TssId = scuId,
            CertificateValidUntil = certUntil,
            Cached = cached,
            OperationalHealth = operationalHealth,
            LastErrorMessageSafe = snap.LastErrorMessageSafe,
            NonFiscalPendingQueueCount = queueCount,
            EstimatedRecoveryTimeUtc = snap.EstimatedRecoveryTimeUtc,
            LastSuccessfulPingUtc = snap.LastSuccessfulPingUtc,
        };
    }

    private static string? ResolveScuId(Tenant? tenant, TseDevice? device)
    {
        if (!string.IsNullOrWhiteSpace(tenant?.TseScuId))
            return tenant.TseScuId.Trim();

        if (device is null)
            return null;

        if (!string.Equals(device.DeviceType, "fiskaly", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(device.Provider, TseOptions.ProviderFiskaly, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(device.DeviceId))
            return device.DeviceId.Trim();

        return string.IsNullOrWhiteSpace(device.SerialNumber) ? null : device.SerialNumber.Trim();
    }

    private static (string Status, string Message, string OperationalHealth) Classify(
        TseHealthSnapshot snap,
        Tenant? tenant,
        TseDevice? device,
        bool cached)
    {
        var certExpired = device is not null
            && (string.Equals(device.CertificateStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase)
                || (device.ExpiresAt is { } exp && exp <= DateTime.UtcNow));

        if (certExpired)
        {
            return (
                PosTseIndicatorStatuses.Inactive,
                "TSE certificate is expired.",
                TseOperationalHealth.Offline.ToString());
        }

        if (snap.Status == TseOperationalHealth.Offline && !cached)
        {
            return (
                PosTseIndicatorStatuses.Inactive,
                snap.LastErrorMessageSafe ?? "TSE is not available.",
                TseOperationalHealth.Offline.ToString());
        }

        if (snap.Status == TseOperationalHealth.Offline && cached)
        {
            return (
                PosTseIndicatorStatuses.Degraded,
                "TSE probe failed; last known status is cached.",
                TseOperationalHealth.Degraded.ToString());
        }

        if (snap.Status == TseOperationalHealth.Degraded)
        {
            return (
                PosTseIndicatorStatuses.Degraded,
                snap.LastErrorMessageSafe ?? "TSE is degraded.",
                TseOperationalHealth.Degraded.ToString());
        }

        if (string.Equals(tenant?.TseStatus, TenantTseStatuses.SoftFallback, StringComparison.OrdinalIgnoreCase))
        {
            return (
                PosTseIndicatorStatuses.Degraded,
                "Fiskaly unavailable; Soft TSE fallback is active.",
                TseOperationalHealth.Degraded.ToString());
        }

        if (device is null)
        {
            return (
                PosTseIndicatorStatuses.Inactive,
                "No TSE device configured for this tenant.",
                TseOperationalHealth.Offline.ToString());
        }

        var softwareSigner = string.Equals(device.DeviceType, "Fake", StringComparison.OrdinalIgnoreCase)
            || string.Equals(device.DeviceType, "Soft", StringComparison.OrdinalIgnoreCase);

        if (!device.IsConnected && !device.CanCreateInvoices && !softwareSigner)
        {
            return (
                PosTseIndicatorStatuses.Inactive,
                device.ErrorMessage ?? "TSE device is not connected.",
                TseOperationalHealth.Offline.ToString());
        }

        return (
            PosTseIndicatorStatuses.Active,
            "TSE is operational.",
            TseOperationalHealth.Online.ToString());
    }
}
