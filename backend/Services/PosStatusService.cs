using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Rksv;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PosStatusService : IPosStatusService
{
    private readonly ILicenseService _licenseService;
    private readonly IPosCashRegisterReadinessService _cashRegisterReadiness;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly AppDbContext _db;

    public PosStatusService(
        ILicenseService licenseService,
        IPosCashRegisterReadinessService cashRegisterReadiness,
        IRksvEnvironmentService rksvEnvironment,
        AppDbContext db)
    {
        _licenseService = licenseService;
        _cashRegisterReadiness = cashRegisterReadiness;
        _rksvEnvironment = rksvEnvironment;
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
        var mandantStatus = await mandantTask.ConfigureAwait(false);
        var licenseDto = LicensePublicStatusMapper.ApplyMandantOverlay(
            LicensePublicStatusMapper.MapDeploymentStatus(deployment),
            mandantStatus);

        var healthSnapshot = _licenseService.GetStatus();
        var healthLicense = BuildHealthLicenseSnapshot(healthSnapshot, licenseDto);

        var cashRegister = await _cashRegisterReadiness.GetReadinessSnapshotForPosAsync(
            userId,
            principal,
            cancellationToken).ConfigureAwait(false);
        var settings = await LoadSettingsSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PosStatusOverviewDto
        {
            ServerTimeUtc = DateTime.UtcNow,
            License = licenseDto,
            HealthLicense = healthLicense,
            CashRegister = cashRegister,
            Settings = settings,
            RksvEnvironment = RksvEnvironmentStatusDto.FromService(_rksvEnvironment),
        };
    }

    /// <summary>
    /// POS overview always applies mandant overlay; health subset must match license badge.
    /// </summary>
    private static PosStatusLicenseHealthDto BuildHealthLicenseSnapshot(
        LicenseStatusResponse deploymentHealth,
        LicensePublicStatusDto licenseDto) =>
        new()
        {
            IsValid = licenseDto.IsValid,
            IsTrial = string.Equals(licenseDto.LicenseType, "Trial", StringComparison.OrdinalIgnoreCase),
            IsExpired = licenseDto.IsExpired,
            DaysRemaining = licenseDto.DaysRemaining,
            ExpiryDate = licenseDto.ValidUntil.HasValue
                ? DateTime.SpecifyKind(licenseDto.ValidUntil.Value, DateTimeKind.Utc)
                : null,
            MachineHash = deploymentHealth.MachineHash,
        };

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
