using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantOperationModesTests
{
    [Theory]
    [InlineData("active", true)]
    [InlineData("readonly", true)]
    [InlineData("maintenance", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void IsKnown_returns_expected(string? mode, bool expected)
    {
        Assert.Equal(expected, TenantOperationModes.IsKnown(mode));
    }

    [Theory]
    [InlineData("READONLY", "readonly")]
    [InlineData("Maintenance", "maintenance")]
    [InlineData("bogus", "active")]
    [InlineData(null, "active")]
    public void Normalize_maps_known_modes_and_defaults_unknown(string? mode, string expected)
    {
        Assert.Equal(expected, TenantOperationModes.Normalize(mode));
    }

    [Fact]
    public void IsMaintenanceActive_false_when_not_maintenance()
    {
        var now = DateTime.UtcNow;
        Assert.False(TenantOperationModes.IsMaintenanceActive("active", now.AddHours(1), now));
        Assert.False(TenantOperationModes.IsMaintenanceActive("readonly", null, now));
    }

    [Fact]
    public void IsMaintenanceActive_true_when_open_ended_or_future_end()
    {
        var now = DateTime.UtcNow;
        Assert.True(TenantOperationModes.IsMaintenanceActive("maintenance", null, now));
        Assert.True(TenantOperationModes.IsMaintenanceActive("maintenance", now.AddHours(2), now));
    }

    [Fact]
    public void IsMaintenanceActive_false_when_window_expired()
    {
        var now = DateTime.UtcNow;
        Assert.False(TenantOperationModes.IsMaintenanceActive("maintenance", now.AddMinutes(-1), now));
    }
}
