using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseStatusInfoBuilderTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_WhenLicenseValid_ReturnsActiveFullAccess()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(30), nowUtc: Now);

        Assert.True(info.IsActive);
        Assert.Equal(30, info.DaysRemaining);
        Assert.Equal(0, info.DaysOverdue);
        Assert.False(info.IsInGracePeriod);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
    }

    [Fact]
    public void Build_WhenWithinWarningWindow_IncludesExpiryWarningMessage()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(10), nowUtc: Now);

        Assert.True(info.IsActive);
        Assert.Equal(10, info.DaysRemaining);
        Assert.Contains("expires in 10 day(s)", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenInGracePeriod_AllowsTransactions()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(-5), nowUtc: Now);

        Assert.False(info.IsActive);
        Assert.Equal(5, info.DaysOverdue);
        Assert.True(info.IsInGracePeriod);
        Assert.Equal(16, info.GracePeriodRemaining);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.Contains("Grace period: 16 day(s) remaining", info.StatusMessage);
    }

    [Fact]
    public void Build_WhenGraceExpired_ReturnsBlocked()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(-22), nowUtc: Now);

        Assert.False(info.IsActive);
        Assert.Equal(22, info.DaysOverdue);
        Assert.False(info.IsInGracePeriod);
        Assert.Equal(0, info.GracePeriodRemaining);
        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.Contains("Access is blocked", info.StatusMessage);
    }

    [Fact]
    public void Build_WhenNoLicense_ReturnsBlocked()
    {
        var info = LicenseStatusInfoBuilder.Build(null, nowUtc: Now);

        Assert.False(info.IsActive);
        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.Contains("No tenant license", info.StatusMessage);
    }

    [Fact]
    public void Build_WhenSuperAdmin_OverridesBlockedTenant()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(-60), isSuperAdmin: true, nowUtc: Now);

        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
    }

    [Fact]
    public void Build_GracePeriodLength_MatchesConfig()
    {
        var lastGraceDay = LicenseStatusInfoBuilder.Build(
            Now.AddDays(-LicenseGracePeriodConfig.GracePeriodDays),
            nowUtc: Now);
        var firstBlockedDay = LicenseStatusInfoBuilder.Build(
            Now.AddDays(-(LicenseGracePeriodConfig.GracePeriodDays + 1)),
            nowUtc: Now);

        Assert.True(lastGraceDay.CanTransact);
        Assert.Equal(0, lastGraceDay.GracePeriodRemaining);
        Assert.False(firstBlockedDay.CanTransact);
    }
}
