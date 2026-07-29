using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SignaturkarteProgramMilestonesTests
{
    [Theory]
    [InlineData(180, "180d")]
    [InlineData(90, "90d")]
    [InlineData(30, "30d")]
    [InlineData(7, "7d")]
    [InlineData(-1, SignaturkarteProgramMilestones.Overdue)]
    [InlineData(0, null)]
    [InlineData(8, null)]
    [InlineData(179, null)]
    public void ResolveMilestone_MatchesConfiguredAnchors(int daysUntil, string? expected)
    {
        var actual = SignaturkarteProgramMilestones.ResolveMilestone(
            daysUntil,
            [180, 90, 30, 7],
            sendOverdue: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveMilestone_DisablesOverdueWhenConfigured()
    {
        Assert.Null(
            SignaturkarteProgramMilestones.ResolveMilestone(-3, [180, 90, 30, 7], sendOverdue: false));
    }

    [Fact]
    public void DaysUntilDeadline_UsesUtcCalendarDates()
    {
        var deadline = new DateTime(2027, 5, 31, 21, 59, 59, DateTimeKind.Utc);
        var now = new DateTime(2026, 12, 2, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(180, SignaturkarteProgramMilestones.DaysUntilDeadline(deadline, now));
    }

    [Fact]
    public void BuildDedupKey_IncludesDeadlineAndScope()
    {
        var key = SignaturkarteProgramMilestones.BuildDedupKey(
            new DateTime(2027, 5, 31, 21, 59, 59, DateTimeKind.Utc),
            "90d",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            new DateTime(2027, 3, 2, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(
            "signaturkarte-program:20270531:90d:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            key);
    }

    [Fact]
    public void BuildDedupKey_OverdueIncludesCalendarDay()
    {
        var key = SignaturkarteProgramMilestones.BuildDedupKey(
            new DateTime(2027, 5, 31, 21, 59, 59, DateTimeKind.Utc),
            SignaturkarteProgramMilestones.Overdue,
            "platform",
            new DateTime(2027, 6, 2, 8, 0, 0, DateTimeKind.Utc));
        Assert.Contains(":overdue:platform:2027-06-02", key);
        Assert.StartsWith("signaturkarte-program:20270531:", key);
    }

    [Theory]
    [InlineData(120, 5, "info")]
    [InlineData(45, 5, "warning")]
    [InlineData(7, 5, "critical")]
    [InlineData(0, 5, "critical")]
    [InlineData(-3, 5, "critical")]
    [InlineData(120, 0, null)]
    public void BannerSeverity_MatchesProductWindows(int days, int open, string? expected)
    {
        Assert.Equal(expected, SignaturkarteProgramMilestones.BannerSeverity(days, open));
    }

    [Fact]
    public void EventTypeFor_MapsOverdueSeparately()
    {
        Assert.Equal(
            ActivityEventType.SignaturkarteProgramReminder,
            SignaturkarteProgramMilestones.EventTypeFor("30d"));
        Assert.Equal(
            ActivityEventType.SignaturkarteProgramOverdue,
            SignaturkarteProgramMilestones.EventTypeFor(SignaturkarteProgramMilestones.Overdue));
    }
}

public sealed class SignaturkarteProgramClassifierTests
{
    [Fact]
    public void Classify_Open_WhenActiveProductionWithoutFlag()
    {
        var device = new TseDevice
        {
            SerialNumber = "SCU-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            IsActive = true,
            CertificateStatus = "VALID",
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Open,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: true));
    }

    [Fact]
    public void Classify_Compliant_WhenFlagSet()
    {
        var device = new TseDevice
        {
            SerialNumber = "SCU-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            IsActive = true,
            CertificateStatus = "VALID",
            SignaturkarteProgramCompliantAtUtc = DateTime.UtcNow,
            SignaturkarteProgramCompliantBy = "user-1",
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Compliant,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: true));
    }

    [Theory]
    [InlineData("soft")]
    [InlineData("fake")]
    public void Classify_Excluded_SoftOrFakeProvider(string provider)
    {
        var device = new TseDevice
        {
            SerialNumber = "X",
            DeviceType = "demo",
            Provider = provider,
            IsActive = true,
            CertificateStatus = "VALID",
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Excluded,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: true));
    }

    [Fact]
    public void Classify_SoftNotExcluded_WhenFlagOff()
    {
        var device = new TseDevice
        {
            SerialNumber = "SOFT-1",
            DeviceType = "SoftTSE",
            Provider = "soft",
            IsActive = true,
            CertificateStatus = "VALID",
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Open,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: false));
    }

    [Fact]
    public void Classify_Revoked_WhenInactive()
    {
        var device = new TseDevice
        {
            SerialNumber = "SCU-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            IsActive = false,
            CertificateStatus = "VALID",
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Revoked,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: true));
    }

    [Fact]
    public void Classify_DoesNotUseExpiresAt()
    {
        var device = new TseDevice
        {
            SerialNumber = "SCU-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            IsActive = true,
            CertificateStatus = "VALID",
            ExpiresAt = DateTime.UtcNow.AddYears(5),
        };
        Assert.Equal(
            SignaturkarteProgramStatuses.Open,
            SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft: true));
    }
}
