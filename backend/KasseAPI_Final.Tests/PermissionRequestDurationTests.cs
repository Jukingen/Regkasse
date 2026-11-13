using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PermissionRequestDurationTests
{
    [Theory]
    [InlineData(PermissionRequestDurations.OneDay, 1)]
    [InlineData(PermissionRequestDurations.SevenDays, 7)]
    [InlineData(PermissionRequestDurations.ThirtyDays, 30)]
    [InlineData("unknown", 7)]
    public void ResolveExpiresAt_PresetDurations(string duration, int expectedDays)
    {
        var now = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var expires = PermissionRequestDurations.ResolveExpiresAt(duration, now, null);
        Assert.Equal(now.AddDays(expectedDays), expires);
    }

    [Fact]
    public void ResolveExpiresAt_Custom_UsesProvidedInstant()
    {
        var now = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var custom = now.AddHours(36);
        var expires = PermissionRequestDurations.ResolveExpiresAt(
            PermissionRequestDurations.Custom,
            now,
            custom);
        Assert.Equal(custom, expires);
    }

    [Fact]
    public void ResolveExpiresAt_CustomWithoutValue_FallsBackToSevenDays()
    {
        var now = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var expires = PermissionRequestDurations.ResolveExpiresAt(
            PermissionRequestDurations.Custom,
            now,
            null);
        Assert.Equal(now.AddDays(7), expires);
    }
}
