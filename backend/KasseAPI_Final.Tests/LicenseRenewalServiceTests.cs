using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseRenewalServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"license_renewal_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static Tenant SeedTenant(AppDbContext db, DateTime? licenseValidUntilUtc)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Renewal Tenant",
            Slug = "renewal-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = licenseValidUntilUtc,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    private static LicenseRenewalService CreateService(
        AppDbContext db,
        LicenseStatusInfo statusInfo)
    {
        var licenseService = new Mock<ILicenseService>();
        licenseService
            .Setup(x => x.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusInfo);

        return new LicenseRenewalService(
            db,
            licenseService.Object,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<LicenseRenewalService>>());
    }

    [Fact]
    public async Task RenewLicenseAsync_InGracePeriod_DeductsUsedGraceDays()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, DateTime.UtcNow.Date.AddDays(-10));
        var service = CreateService(
            db,
            new LicenseStatusInfo
            {
                IsInGracePeriod = true,
                DaysOverdue = 10,
                ValidUntil = DateTime.UtcNow.Date.AddDays(-10),
                DaysRemaining = -10,
            });

        var result = await service.RenewLicenseAsync(tenant.Id, additionalMonths: 12);

        Assert.True(result.Success);
        Assert.Equal(10, result.DaysDeducted);
        Assert.Equal(360, result.DaysAdded);
        var expected = DateTime.UtcNow.Date.AddDays(350);
        Assert.Equal(expected, result.NewExpiryDate!.Value.Date);
    }

    [Fact]
    public async Task RenewLicenseAsync_StillValid_AddsMonthsToExistingExpiry()
    {
        await using var db = CreateDb();
        var validUntil = DateTime.UtcNow.Date.AddDays(20);
        var tenant = SeedTenant(db, validUntil);
        var service = CreateService(
            db,
            new LicenseStatusInfo
            {
                DaysRemaining = 20,
                ValidUntil = validUntil,
            });

        var result = await service.RenewLicenseAsync(tenant.Id, additionalMonths: 3);

        Assert.True(result.Success);
        Assert.Equal(0, result.DaysDeducted);
        Assert.Equal(validUntil.AddMonths(3).Date, result.NewExpiryDate!.Value.Date);
    }

    [Fact]
    public async Task RenewLicenseAsync_ExpiredBeyondGrace_StartsFreshFromToday()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, DateTime.UtcNow.Date.AddDays(-40));
        var service = CreateService(
            db,
            new LicenseStatusInfo
            {
                DaysRemaining = -40,
                DaysOverdue = 40,
                ValidUntil = DateTime.UtcNow.Date.AddDays(-40),
                RequiresRenewal = true,
            });

        var result = await service.RenewLicenseAsync(tenant.Id, additionalMonths: 1);

        Assert.True(result.Success);
        Assert.Equal(0, result.DaysDeducted);
        Assert.Equal(DateTime.UtcNow.Date.AddMonths(1), result.NewExpiryDate!.Value.Date);
    }

    [Fact]
    public async Task RenewLicenseAsync_TenantNotFound_ReturnsFailure()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new LicenseStatusInfo());

        var result = await service.RenewLicenseAsync(Guid.NewGuid(), additionalMonths: 1);

        Assert.False(result.Success);
        Assert.Equal("Tenant not found", result.Message);
    }
}
