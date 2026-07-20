using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseStatusInfoBuilderTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_WhenLicenseValid_ReturnsActiveFullAccess()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(30), nowUtc: Now, language: "en");

        Assert.True(info.IsActive);
        Assert.False(info.IsExpired);
        Assert.False(info.IsLocked);
        Assert.Equal(30, info.DaysRemaining);
        Assert.Equal(0, info.DaysOverdue);
        Assert.False(info.IsInGracePeriod);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.Equal(LicenseStatusMessageKeys.Active, info.StatusMessageKey);
        Assert.Empty(info.Restrictions);
    }

    [Fact]
    public void Build_WhenWithinWarningWindow_IncludesExpiryWarningMessage()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(10), nowUtc: Now, language: "en");

        Assert.True(info.IsActive);
        Assert.Equal(10, info.DaysRemaining);
        Assert.Equal(LicenseStatusMessageKeys.ExpiringSoon, info.StatusMessageKey);
        Assert.Contains("expires in 10 day(s)", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenInGracePeriod_AllowsTransactionsAndReportsLockDate()
    {
        var validUntil = Now.AddDays(-5);
        var info = LicenseStatusInfoBuilder.Build(validUntil, nowUtc: Now, language: "en");

        Assert.False(info.IsActive);
        Assert.True(info.IsExpired);
        Assert.False(info.IsLocked);
        Assert.Equal(5, info.DaysOverdue);
        Assert.True(info.IsInGracePeriod);
        Assert.Equal(LicenseGracePeriodConfig.GracePeriodDays - 5, info.GracePeriodRemaining);
        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.Equal(LicenseStatusMessageKeys.Grace, info.StatusMessageKey);
        Assert.Equal(validUntil.AddDays(LicenseGracePeriodConfig.GracePeriodDays), info.LockDate);
        Assert.Contains("POS can still be used", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LicenseStatusRestrictionCodes.PosOperational, info.Restrictions);
        Assert.Contains(LicenseStatusRestrictionCodes.LockPending, info.Restrictions);
    }

    [Fact]
    public void Build_WhenGraceExpired_ReturnsLocked()
    {
        var info = LicenseStatusInfoBuilder.Build(
            Now.AddDays(-(LicenseGracePeriodConfig.GracePeriodDays + 1)),
            nowUtc: Now,
            language: "en");

        Assert.False(info.IsActive);
        Assert.True(info.IsExpired);
        Assert.True(info.IsLocked);
        Assert.Equal(LicenseGracePeriodConfig.GracePeriodDays + 1, info.DaysOverdue);
        Assert.False(info.IsInGracePeriod);
        Assert.Equal(0, info.GracePeriodRemaining);
        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.Equal(LicenseStatusMessageKeys.Locked, info.StatusMessageKey);
        Assert.Contains("POS is locked", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LicenseStatusRestrictionCodes.PosLocked, info.Restrictions);
        Assert.Contains(LicenseStatusRestrictionCodes.SuperAdminUnlockOnly, info.Restrictions);
    }

    [Fact]
    public void Build_WhenNoLicense_ReturnsBlocked()
    {
        var info = LicenseStatusInfoBuilder.Build(null, nowUtc: Now, language: "en");

        Assert.False(info.IsActive);
        Assert.False(info.CanAccess);
        Assert.False(info.CanTransact);
        Assert.Equal(LicenseStatusMessageKeys.None, info.StatusMessageKey);
        Assert.Contains("No tenant license", info.StatusMessage);
    }

    [Fact]
    public void Build_WhenSuperAdmin_OverridesBlockedTenant()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(-60), isSuperAdmin: true, nowUtc: Now);

        Assert.True(info.CanAccess);
        Assert.True(info.CanTransact);
        Assert.False(info.IsLocked);
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
        Assert.True(firstBlockedDay.IsLocked);
    }

    [Fact]
    public void Build_GermanGraceMessage_IncludesLockDate()
    {
        var info = LicenseStatusInfoBuilder.Build(Now.AddDays(-2), nowUtc: Now, language: "de");

        Assert.Equal(LicenseStatusMessageKeys.Grace, info.StatusMessageKey);
        Assert.Contains("gesperrt", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tag", info.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
