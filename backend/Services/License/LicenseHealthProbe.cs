using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.License;

/// <summary>Anonymous <c>GET /api/health/license</c> payload (additive fields; existing clients keep working).</summary>
public sealed class LicenseHealthStatusDto
{
    /// <summary><c>Healthy</c>, <c>Degraded</c>, or <c>Unhealthy</c>.</summary>
    public string Status { get; init; } = LicenseHealthProbe.Healthy;

    public bool DatabaseConnected { get; init; }

    public bool IssuedLicensesTableOk { get; init; }

    public bool LicenseSalesTableOk { get; init; }

    public bool SampleQueryOk { get; init; }

    public string HeaderStatus { get; init; } = string.Empty;

    public bool IsValid { get; init; }

    public bool IsTrial { get; init; }

    public bool IsExpired { get; init; }

    public int DaysRemaining { get; init; }

    public DateTime? ExpiryDate { get; init; }

    public string MachineHash { get; init; } = string.Empty;

    public object? Reminders { get; init; }
}

/// <summary>Probes DB tables plus in-process deployment snapshot for license health.</summary>
public static class LicenseHealthProbe
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unhealthy = "Unhealthy";

    public static async Task<LicenseHealthStatusDto> CheckAsync(
        ILicenseService licenseService,
        ILicenseReminderNotificationStore reminders,
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(licenseService);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(db);

        var databaseConnected = false;
        var issuedOk = false;
        var salesOk = false;
        try
        {
            databaseConnected = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (databaseConnected)
            {
                _ = await db.IssuedLicenses.AsNoTracking()
                    .Select(x => x.Id)
                    .Take(1)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                issuedOk = true;

                _ = await db.LicenseSales.IgnoreQueryFilters().AsNoTracking()
                    .Select(x => x.Id)
                    .Take(1)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                salesOk = true;
            }
        }
        catch
        {
            databaseConnected = false;
            issuedOk = false;
            salesOk = false;
        }

        var sampleQueryOk = issuedOk && salesOk;
        var snapshot = licenseService.GetStatus();
        var snapshotOk = licenseService.IsLicenseSnapshotInitialized;
        var overall = !databaseConnected || !sampleQueryOk
            ? Unhealthy
            : snapshotOk
                ? Healthy
                : Degraded;

        var headerStatus = LicenseMiddleware.ResolveLicenseHeaderStatus(
            snapshot,
            licenseService.IsLicenseSnapshotInitialized);

        return new LicenseHealthStatusDto
        {
            Status = overall,
            DatabaseConnected = databaseConnected,
            IssuedLicensesTableOk = issuedOk,
            LicenseSalesTableOk = salesOk,
            SampleQueryOk = sampleQueryOk,
            HeaderStatus = headerStatus,
            IsValid = snapshot.IsValid,
            IsTrial = snapshot.IsTrial,
            IsExpired = snapshot.IsExpired,
            DaysRemaining = snapshot.DaysRemaining,
            ExpiryDate = snapshot.ExpiryDate,
            MachineHash = snapshot.MachineHash ?? string.Empty,
            Reminders = reminders.GetReminders(),
        };
    }
}
