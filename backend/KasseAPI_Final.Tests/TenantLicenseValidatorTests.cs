using KasseAPI_Final.Services.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLicenseValidatorTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);
    private readonly TenantLicenseValidator _sut = new();

    [Fact]
    public void GetStatus_WhenLicenseStillValid_ReturnsActive()
    {
        var status = _sut.GetStatus(Now.AddDays(1), Now);
        Assert.Equal(TenantLicenseStatus.Active, status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(21)]
    public void GetStatus_WhenExpiredWithinGracePeriod_ReturnsGraceWrite(int daysExpired)
    {
        var status = _sut.GetStatus(Now.AddDays(-daysExpired), Now);
        Assert.Equal(TenantLicenseStatus.GraceWrite, status);
    }

    [Theory]
    [InlineData(22)]
    [InlineData(90)]
    public void GetStatus_WhenExpiredBeyondGracePeriod_ReturnsLockdown(int daysExpired)
    {
        var status = _sut.GetStatus(Now.AddDays(-daysExpired), Now);
        Assert.Equal(TenantLicenseStatus.Lockdown, status);
    }

    [Fact]
    public void GetPermissions_WhenLockdown_DeniesAllAccessForNormalUsers()
    {
        var permissions = _sut.GetPermissions(Now.AddDays(-45), isSuperAdmin: false, Now);
        Assert.False(permissions.CanWrite);
        Assert.False(permissions.CanManageUsers);
        Assert.False(permissions.CanAccess);
    }

    [Fact]
    public void GetPermissions_WhenSuperAdmin_AlwaysAllowsAccess()
    {
        var permissions = _sut.GetPermissions(Now.AddDays(-120), isSuperAdmin: true, Now);
        Assert.True(permissions.CanWrite);
        Assert.True(permissions.CanManageUsers);
        Assert.True(permissions.CanAccess);
    }
}
