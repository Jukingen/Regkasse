using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
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
        Assert.Equal("Aktive Lizenz", info.StatusMessage);
        Assert.False(info.RequiresRenewal);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenWithinWarningWindow_UsesGermanWarningCopy()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(10);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.Equal(10, info.DaysRemaining);
        Assert.Contains("läuft in 10 Tagen ab", info.StatusMessage);
        Assert.False(info.RequiresRenewal);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenInGracePeriod_AllowsAccessAndTransactions()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(-5);
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.True(info.IsInGracePeriod);
        Assert.Equal(5, info.DaysOverdue);
        Assert.Equal(16, info.GracePeriodRemaining);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.False(info.RequiresRenewal);
        Assert.Contains("Grace Period", info.StatusMessage);
    }

    [Fact]
    public void BuildTenantLicenseStatusInfo_WhenGraceExpired_BlocksAndRequiresRenewal()
    {
        var validUntil = DateTime.UtcNow.Date.AddDays(-(LicenseGracePeriodConfig.GracePeriodDays + 1));
        var info = LicenseService.BuildTenantLicenseStatusInfo(CreateTenant(validUntil));

        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.True(info.RequiresRenewal);
        Assert.Contains("Zugang gesperrt", info.StatusMessage);
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
    public async Task GetLicenseStatusAsync_InDevelopment_ReturnsUnlimitedAccess_EvenWhenTenantExpired()
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

        Assert.True(info.IsActive);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.Equal(999, info.DaysRemaining);
        Assert.False(info.IsInGracePeriod);
        Assert.Equal("Development Mode - Unlimited Access", info.StatusMessage);
    }
}
