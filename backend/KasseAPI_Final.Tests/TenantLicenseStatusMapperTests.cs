using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLicenseStatusMapperTests
{
    [Fact]
    public void ComputeKindAndDays_TrialWith30DaysRemaining_ReturnsTrial()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var until = now.AddDays(30);

        var (days, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(until, licenseKey: null, now);

        Assert.Equal("trial", kind);
        Assert.Equal(30, days);
    }

    [Fact]
    public void TryMapToLicenseStatus_Trial_MatchesDeploymentTrialShape()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant
        {
            Id = DemoTenantIds.Bar,
            Slug = "bar",
            Name = "Test Bar",
            LicenseValidUntilUtc = now.AddDays(30),
            LicenseKey = null,
        };

        var status = TenantLicenseStatusMapper.TryMapToLicenseStatus(tenant, "machine-hash", now);

        Assert.NotNull(status);
        Assert.True(status.IsTrial);
        Assert.False(status.IsValid);
        Assert.False(status.IsExpired);
        Assert.Equal(30, status.DaysRemaining);
        Assert.Equal(tenant.LicenseValidUntilUtc, status.ExpiryDate);
    }

    [Fact]
    public void TryMapToLicenseStatus_Expired_ReturnsZeroDays()
    {
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        var tenant = new Tenant
        {
            Id = DemoTenantIds.Bar,
            Slug = "bar",
            Name = "Test Bar",
            LicenseValidUntilUtc = now.AddDays(-1),
        };

        var status = TenantLicenseStatusMapper.TryMapToLicenseStatus(tenant, "machine-hash", now);

        Assert.NotNull(status);
        Assert.True(status.IsExpired);
        Assert.Equal(0, status.DaysRemaining);
    }
}
