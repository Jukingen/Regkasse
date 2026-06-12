using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PosStatusService : IPosStatusService
{
    private readonly ILicenseService _licenseService;
    private readonly IPosCashRegisterReadinessService _cashRegisterReadiness;
    private readonly AppDbContext _db;

    public PosStatusService(
        ILicenseService licenseService,
        IPosCashRegisterReadinessService cashRegisterReadiness,
        AppDbContext db)
    {
        _licenseService = licenseService;
        _cashRegisterReadiness = cashRegisterReadiness;
        _db = db;
    }

    public async Task<PosStatusOverviewDto> GetOverviewAsync(
        string userId,
        ClaimsPrincipal principal,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // License lookups use their own DbContext factory; POS register + settings share scoped AppDbContext.
        var deploymentTask = _licenseService.GetCurrentStatusAsync(cancellationToken);
        var mandantTask = _licenseService.GetLicenseStatusAsync(tenantId, cancellationToken);
        await Task.WhenAll(deploymentTask, mandantTask).ConfigureAwait(false);

        var deployment = await deploymentTask.ConfigureAwait(false);
        var licenseDto = LicensePublicStatusMapper.MapDeploymentStatus(deployment);
        licenseDto = LicensePublicStatusMapper.ApplyMandantOverlay(
            licenseDto,
            await mandantTask.ConfigureAwait(false));

        var healthSnapshot = _licenseService.GetStatus();

        var cashRegister = await _cashRegisterReadiness.GetReadinessSnapshotForPosAsync(
            userId,
            principal,
            cancellationToken).ConfigureAwait(false);
        var settings = await LoadSettingsSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PosStatusOverviewDto
        {
            ServerTimeUtc = DateTime.UtcNow,
            License = licenseDto,
            HealthLicense = new PosStatusLicenseHealthDto
            {
                IsValid = healthSnapshot.IsValid,
                IsTrial = healthSnapshot.IsTrial,
                IsExpired = healthSnapshot.IsExpired,
                DaysRemaining = healthSnapshot.DaysRemaining,
                ExpiryDate = healthSnapshot.ExpiryDate.HasValue
                    ? DateTime.SpecifyKind(healthSnapshot.ExpiryDate.Value, DateTimeKind.Utc)
                    : null,
                MachineHash = healthSnapshot.MachineHash,
            },
            CashRegister = cashRegister,
            Settings = settings,
        };
    }

    private async Task<PosStatusSettingsSnapshotDto> LoadSettingsSnapshotAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var row = await _db.UserSettings
            .AsNoTracking()
            .Where(us => us.UserId == userId)
            .Select(us => new { us.CashRegisterId, us.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            var now = DateTime.UtcNow;
            return new PosStatusSettingsSnapshotDto
            {
                CashRegisterId = null,
                SettingsVersion = now.Ticks,
                UpdatedAtUtc = now,
            };
        }

        var updatedUtc = DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc);
        return new PosStatusSettingsSnapshotDto
        {
            CashRegisterId = string.IsNullOrWhiteSpace(row.CashRegisterId) ? null : row.CashRegisterId.Trim(),
            SettingsVersion = updatedUtc.Ticks,
            UpdatedAtUtc = updatedUtc,
        };
    }
}
