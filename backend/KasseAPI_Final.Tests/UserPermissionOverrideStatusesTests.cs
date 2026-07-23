using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserPermissionOverrideStatusesTests
{
    [Fact]
    public void Compute_Expired_WhenExpiresAtInPast()
    {
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var status = UserPermissionOverrideStatuses.Compute(null, now.AddMinutes(-1), now, 48);
        Assert.Equal(UserPermissionOverrideStatuses.Expired, status);
    }

    [Fact]
    public void Compute_Scheduled_WhenValidFromInFuture()
    {
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var status = UserPermissionOverrideStatuses.Compute(now.AddHours(2), now.AddDays(7), now, 48);
        Assert.Equal(UserPermissionOverrideStatuses.Scheduled, status);
    }

    [Fact]
    public void Compute_ExpiringSoon_WithinWindow()
    {
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var status = UserPermissionOverrideStatuses.Compute(null, now.AddHours(24), now, 48);
        Assert.Equal(UserPermissionOverrideStatuses.ExpiringSoon, status);
    }

    [Fact]
    public void Compute_Active_Otherwise()
    {
        var now = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var status = UserPermissionOverrideStatuses.Compute(null, now.AddDays(10), now, 48);
        Assert.Equal(UserPermissionOverrideStatuses.Active, status);
    }
}
