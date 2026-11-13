using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseServiceGetLicenseStatusTests
{
    private static Tenant CreateTenant(DateTime? licenseValidUntilUtc, bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Status Tenant",
            Slug = "status-tenant",
            Status = TenantStatuses.Active,
            IsActive = isActive,
            LicenseValidUntilUtc = licenseValidUntilUtc,
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenNoExpiry_ReturnsUnlimitedTrialShape()
    {
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(null));

        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.Equal(999, info.DaysRemaining);
        Assert.Equal("Lizenz aktiv", info.StatusMessage);
        Assert.Equal(LicenseStatusMessageKeys.Active, info.StatusMessageKey);
        Assert.False(info.RequiresRenewal);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenWithinWarningWindow_UsesGermanWarningCopy()
    {
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var validUntil = now.AddDays(10);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil), now);

        Assert.Equal(10, info.DaysRemaining);
        Assert.Equal(validUntil, info.ValidUntil);
        Assert.Contains("läuft in 10 Tag", info.StatusMessage);
        Assert.Equal(LicenseStatusMessageKeys.ExpiringSoon, info.StatusMessageKey);
        Assert.False(info.RequiresRenewal);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_PreservesFullValidUntilTimestamp()
    {
        var now = new DateTime(2026, 7, 16, 19, 15, 0, DateTimeKind.Utc);
        var validUntil = new DateTime(2026, 7, 17, 16, 36, 0, DateTimeKind.Utc);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil), now);

        Assert.Equal(validUntil, info.ValidUntil);
        Assert.NotEqual(validUntil.Date, info.ValidUntil);
        Assert.Equal(1, info.DaysRemaining);
        Assert.True(info.CanAccess);
        Assert.False(info.IsInGracePeriod);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenLessThanOneDayLeft_UsesCeilingDaysNotCalendarMidnight()
    {
        var now = new DateTime(2026, 7, 16, 21, 15, 0, DateTimeKind.Utc);
        // Real expiry later same evening locally; must not collapse to UTC midnight (02:00 CEST).
        var validUntil = new DateTime(2026, 7, 17, 16, 36, 0, DateTimeKind.Utc);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil), now);

        Assert.Equal(validUntil, info.ValidUntil);
        Assert.Equal(1, info.DaysRemaining);
        // Truncating to UTC midnight would have yielded ~4–5 hours; full stamp leaves ~19h.
        Assert.True((validUntil - now).TotalHours > 18);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenInGracePeriod_AllowsAccessAndTransactions()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(-5);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.True(info.IsInGracePeriod);
        Assert.True(info.IsExpired);
        Assert.False(info.IsLocked);
        Assert.Equal(5, info.DaysOverdue);
        Assert.Equal(LicenseGracePeriodConfig.GracePeriodDays - 5, info.GracePeriodRemaining);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.False(info.RequiresRenewal);
        Assert.Equal(LicenseStatusMessageKeys.Grace, info.StatusMessageKey);
        Assert.NotNull(info.LockDate);
        Assert.Contains("gesperrt", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LicenseStatusRestrictionCodes.PosOperational, info.Restrictions);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_GraceRemaining_IsSevenMinusDaysOverdue_NotValidUntilHorizon()
    {
        // Regression: GracePeriodDays must be 7 (not a huge Dev sentinel like 999),
        // otherwise messages show e.g. "997 Tage" (= 999 - 2) instead of "5 Tage".
        LicenseGracePeriodConfig.ApplyFrom(new LicenseOptions
        {
            GracePeriodDays = LicenseGracePeriodConfig.DefaultGracePeriodDays,
            WarningDaysBeforeExpiry = LicenseGracePeriodConfig.DefaultWarningDaysBeforeExpiry,
            ArchiveAfterDays = LicenseGracePeriodConfig.DefaultArchiveAfterDays,
        });
        Assert.Equal(7, LicenseGracePeriodConfig.GracePeriodDays);

        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var validUntil = now.AddDays(-2);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil), now, "de");

        Assert.True(info.IsInGracePeriod);
        Assert.Equal(2, info.DaysOverdue);
        Assert.Equal(5, info.GracePeriodRemaining);
        Assert.Equal(validUntil.AddDays(7), info.LockDate);
        Assert.Contains("noch 5 Tag", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("997", info.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenGraceExpired_BlocksAndRequiresRenewal()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(-(LicenseGracePeriodConfig.GracePeriodDays + 1));
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.True(info.IsLocked);
        Assert.True(info.RequiresRenewal);
        Assert.Equal(LicenseStatusMessageKeys.Locked, info.StatusMessageKey);
        Assert.Contains("gesperrt", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LicenseStatusRestrictionCodes.SuperAdminUnlockOnly, info.Restrictions);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_LastGraceDay_RemainsAccessible()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(-LicenseGracePeriodConfig.GracePeriodDays);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.True(info.IsInGracePeriod);
        Assert.True(info.CanAccess);
        Assert.Equal(0, info.GracePeriodRemaining);
        Assert.False(info.RequiresRenewal);
    }

    [Fact]
    public async Task GetLicenseStatusAsync_InDevelopment_ReturnsPersistedTenantRow()
    {
        var tenantId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.Date.AddDays(1);
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"LicenseStatusDev_{Guid.NewGuid():N}")
                .Options,
            NullCurrentTenantAccessor.Instance);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dev Tenant",
            Slug = "dev-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntil,
            LicenseKey = "TEST-KEY",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(db);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);

        var service = new LicenseService(
            Options.Create(new LicenseOptions()),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILicenseStorageService>(),
            scopeFactory.Object,
            Mock.Of<IActivityEventPublisher>(),
            Mock.Of<ILogger<LicenseService>>(),
            hostEnvironment.Object,
            Options.Create(new TseOptions { TseMode = "Device" }),
            Mock.Of<IOptionsMonitor<DevelopmentOptions>>(o => o.CurrentValue == new DevelopmentOptions()),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

        var info = await service.GetLicenseStatusAsync(tenantId);

        Assert.Equal(1, info.DaysRemaining);
        Assert.True(info.CanAccess);
        Assert.Contains("läuft in 1 Tag", info.StatusMessage);
        Assert.NotEqual(999, info.DaysRemaining);
    }

    [Fact]
    public async Task GetLicenseStatusAsync_InDevelopment_WhenGraceExpired_ReportsLockdown()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"LicenseStatusDev_{Guid.NewGuid():N}")
                .Options,
            NullCurrentTenantAccessor.Instance);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Expired Dev Tenant",
            Slug = "expired-dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(-120),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(db);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);

        var service = new LicenseService(
            Options.Create(new LicenseOptions()),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<ILicenseStorageService>(),
            scopeFactory.Object,
            Mock.Of<IActivityEventPublisher>(),
            Mock.Of<ILogger<LicenseService>>(),
            hostEnvironment.Object,
            Options.Create(new TseOptions { TseMode = "Device" }),
            Mock.Of<IOptionsMonitor<DevelopmentOptions>>(o => o.CurrentValue == new DevelopmentOptions()),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

        var info = await service.GetLicenseStatusAsync(tenantId);

        Assert.False(info.CanAccess);
        Assert.True(info.RequiresRenewal);
        Assert.True(info.IsLocked);
        Assert.Contains("gesperrt", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
