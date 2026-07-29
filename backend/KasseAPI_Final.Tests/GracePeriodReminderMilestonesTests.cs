using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GracePeriodReminderMilestonesTests
{
    [Fact]
    public void ResolveGraceDaysRemaining_MatchesLicenseServiceWholeDayMath()
    {
        var now = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

        // Same calendar day as expiry → treated as 1 day overdue → 6 remaining in 7-day grace.
        Assert.Equal(6, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now, now, 7));

        Assert.Equal(6, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-1), now, 7));
        Assert.Equal(4, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-3), now, 7));
        Assert.Equal(2, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-5), now, 7));
        Assert.Equal(1, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-6), now, 7));
        Assert.Equal(0, GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-7), now, 7));
        Assert.Null(GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(-8), now, 7));
        Assert.Null(GracePeriodReminderMilestones.ResolveGraceDaysRemaining(now.AddDays(1), now, 7));
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(4, true)]
    [InlineData(2, true)]
    [InlineData(5, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(0, true)]
    public void ShouldSendReminder_MatchesDefaultAnchorsAndUrgent(int days, bool expected)
    {
        var should = GracePeriodReminderMilestones.ShouldSendReminder(
            days,
            GracePeriodReminderMilestones.DefaultReminderDays,
            sendUrgent: true,
            urgentDaysInclusive: 1);
        Assert.Equal(expected, should);
    }

    [Fact]
    public void BuildDedupKey_IncludesGraceMarkerAndRemainingDays()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var until = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var key = GracePeriodReminderMilestones.BuildDedupKey(tenantId, until, 4);
        Assert.Equal($"{tenantId:N}_20260720_grace_4", key);
    }
}
