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
        var deploymentTask = _licenseService.GetCurrentStatusAsync(cancellationToken);
        var mandantTask = _licenseService.GetLicenseStatusAsync(tenantId, cancellationToken);
        var cashRegisterTask = _cashRegisterReadiness.GetReadinessSnapshotForPosAsync(
            userId,
            principal,
            cancellationToken);
        var settingsTask = LoadSettingsSnapshotAsync(userId, cancellationToken);

        await Task.WhenAll(deploymentTask, mandantTask, cashRegisterTask, settingsTask).ConfigureAwait(false);

        var deployment = await deploymentTask.ConfigureAwait(false);
        var licenseDto = LicensePublicStatusMapper.MapDeploymentStatus(deployment);
        licenseDto = LicensePublicStatusMapper.ApplyMandantOverlay(
            licenseDto,
            await mandantTask.ConfigureAwait(false));

        var healthSnapshot = _licenseService.GetStatus();

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
            CashRegister = await cashRegisterTask.ConfigureAwait(false),
            Settings = await settingsTask.ConfigureAwait(false),
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
