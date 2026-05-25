using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeploymentLicenseValidatorTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);
    private readonly DeploymentLicenseValidator _sut = new();

    [Fact]
    public void GetStatus_WhenPaidLicenseValid_ReturnsActive()
    {
        var snapshot = new LicenseStatusResponse(true, false, false, 10, Now.AddDays(10), "machine");
        Assert.Equal(DeploymentLicenseStatus.Active, _sut.GetStatus(snapshot, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    public void GetStatus_WhenExpiredWithin15Days_ReturnsGraceWrite(int daysExpired)
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-daysExpired), "machine");
        Assert.Equal(DeploymentLicenseStatus.GraceWrite, _sut.GetStatus(snapshot, Now));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(60)]
    public void GetStatus_WhenExpiredWithin60Days_ReturnsGraceReadOnly(int daysExpired)
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-daysExpired), "machine");
        Assert.Equal(DeploymentLicenseStatus.GraceReadOnly, _sut.GetStatus(snapshot, Now));
    }

    [Fact]
    public void GetStatus_WhenExpiredBeyond60Days_ReturnsLockdown()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, Now.AddDays(-61), "machine");
        Assert.Equal(DeploymentLicenseStatus.Lockdown, _sut.GetStatus(snapshot, Now));
    }

    [Fact]
    public void GetStatus_WhenNoExpiryPresent_ReturnsNoLicense()
    {
        var snapshot = new LicenseStatusResponse(false, false, true, 0, null, "machine");
        Assert.Equal(DeploymentLicenseStatus.NoLicense, _sut.GetStatus(snapshot, Now));
    }
}
