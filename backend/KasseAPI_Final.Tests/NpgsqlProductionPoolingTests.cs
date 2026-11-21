using KasseAPI_Final;
using Npgsql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Covers Npgsql pooling defaults from ApplicationHost.ApplyProductionNpgsqlPooling.
/// </summary>
public sealed class NpgsqlProductionPoolingTests
{
    [Fact]
    public void Applies_defaults_when_pooling_keys_omitted()
    {
        var applied = ApplicationHost.ApplyProductionNpgsqlPooling(
            "Host=db;Database=kasse;Username=u;Password=p");
        var builder = new NpgsqlConnectionStringBuilder(applied);

        Assert.True(builder.Pooling);
        Assert.Equal(5, builder.MinPoolSize);
        Assert.Equal(200, builder.MaxPoolSize);
        Assert.Equal(300, builder.ConnectionLifetime);
        Assert.Equal(300, builder.ConnectionIdleLifetime);
    }

    [Fact]
    public void Explicit_pool_sizes_are_preserved_by_builder()
    {
        var applied = ApplicationHost.ApplyProductionNpgsqlPooling(
            "Host=db;Database=kasse;Username=u;Password=p;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=50;Connection Lifetime=120;Connection Idle Lifetime=60;");
        var builder = new NpgsqlConnectionStringBuilder(applied);

        Assert.True(builder.Pooling);
        Assert.Equal(2, builder.MinPoolSize);
        Assert.Equal(50, builder.MaxPoolSize);
        Assert.Equal(120, builder.ConnectionLifetime);
        Assert.Equal(60, builder.ConnectionIdleLifetime);
    }

    [Fact]
    public void Configured_pool_sizes_apply_when_the_connection_string_omits_them()
    {
        var applied = ApplicationHost.ApplyProductionNpgsqlPooling(
            "Host=db;Database=kasse;Username=u;Password=p",
            maxPoolSize: 80,
            minPoolSize: 4,
            connectionIdleLifetimeSeconds: 120);
        var builder = new NpgsqlConnectionStringBuilder(applied);

        Assert.Equal(4, builder.MinPoolSize);
        Assert.Equal(80, builder.MaxPoolSize);
        Assert.Equal(120, builder.ConnectionIdleLifetime);
    }
}
