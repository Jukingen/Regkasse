using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLicenseStatusMapperTests
{
    [Fact]
    public void ComputeKindAndDays_ActiveLicense_ReturnsActive()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var until = now.AddDays(30);

        var (days, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(until, licenseKey: null, now);

        Assert.Equal("active", kind);
        Assert.Equal(30, days);
    }

    [Fact]
    public void TryMapToLicenseStatus_GraceWrite_RemainsOperational()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant
        {
            Id = DemoTenantIds.Prod,
            Slug = "prod",
            Name = "Test Bar",
            LicenseValidUntilUtc = now.AddDays(-10),
            LicenseKey = "TENANT-KEY",
        };

        var status = TenantLicenseStatusMapper.TryMapToLicenseStatus(tenant, "machine-hash", now);

        Assert.NotNull(status);
        Assert.False(status.IsTrial);
        Assert.True(status.IsValid);
        Assert.False(status.IsExpired);
        Assert.Equal(0, status.DaysRemaining);
        Assert.Equal(tenant.LicenseValidUntilUtc, status.ExpiryDate);
    }

    [Fact]
    public void TryMapToLicenseStatus_LockdownAfterGrace_ReturnsExpired()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant
        {
            Id = DemoTenantIds.Prod,
            Slug = "prod",
            Name = "Test Bar",
            LicenseValidUntilUtc = now.AddDays(-22),
        };

        var status = TenantLicenseStatusMapper.TryMapToLicenseStatus(tenant, "machine-hash", now);

        Assert.NotNull(status);
        Assert.True(status.IsExpired);
        Assert.Equal(0, status.DaysRemaining);
    }
}
