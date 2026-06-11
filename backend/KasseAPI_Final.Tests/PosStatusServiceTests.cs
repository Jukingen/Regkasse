using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosStatusServiceTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;
    private const string UserId = "cashier-status-overview";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosStatusSvc_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantId));
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsLicenseRegisterAndSettingsSnapshot()
    {
        await using var ctx = CreateContext();
        var updatedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CashRegisterId = Guid.NewGuid().ToString("D"),
            UpdatedAt = updatedAt,
            CreatedAt = updatedAt,
        });
        await ctx.SaveChangesAsync();

        var license = new Mock<ILicenseService>();
        license.Setup(s => s.GetCurrentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: 30,
                ExpiryDate: DateTime.UtcNow.AddDays(30),
                MachineHash: "abc123"));
        license.Setup(s => s.GetStatus())
            .Returns(new LicenseStatusResponse(
                IsValid: true,
                IsTrial: false,
                IsExpired: false,
                DaysRemaining: 30,
                ExpiryDate: DateTime.UtcNow.AddDays(30),
                MachineHash: "abc123"));
        license.Setup(s => s.GetLicenseStatusAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LicenseService.CreateUnlimitedMandantLicenseStatus("Aktive Lizenz"));

        var readiness = new Mock<IPosCashRegisterReadinessService>();
        readiness.Setup(s => s.GetReadinessSnapshotForPosAsync(
                UserId,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PosCashRegisterContextDto
            {
                EffectiveRegisterId = Guid.NewGuid().ToString("D"),
                NextAction = "ready",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady,
            });

        var svc = new PosStatusService(license.Object, readiness.Object, ctx);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, UserId) },
            "Test"));

        var overview = await svc.GetOverviewAsync(UserId, principal, TenantId);

        Assert.True(overview.License.IsValid);
        Assert.Equal("Licensed", overview.License.LicenseType);
        Assert.True(overview.HealthLicense.IsValid);
        Assert.Equal("abc123", overview.HealthLicense.MachineHash);
        Assert.Equal("ready", overview.CashRegister.NextAction);
        Assert.NotNull(overview.Settings.CashRegisterId);
        Assert.Equal(updatedAt.Ticks, overview.Settings.SettingsVersion);
        Assert.True((DateTime.UtcNow - overview.ServerTimeUtc).TotalSeconds < 5);
    }
}
