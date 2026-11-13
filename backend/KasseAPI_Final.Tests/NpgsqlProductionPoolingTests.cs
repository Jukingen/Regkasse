using Npgsql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Covers production Npgsql pooling defaults (same rules as ApplicationHost.ApplyProductionNpgsqlPooling).
/// Kept as a pure builder test so we do not need InternalsVisibleTo.
/// </summary>
public sealed class NpgsqlProductionPoolingTests
{
    [Fact]
    public void Applies_defaults_when_pooling_keys_omitted()
    {
        var input = "Host=db;Database=kasse;Username=u;Password=p";
        var builder = new NpgsqlConnectionStringBuilder(input)
        {
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 20,
            ConnectionLifetime = 300
        };

        Assert.True(builder.Pooling);
        Assert.Equal(5, builder.MinPoolSize);
        Assert.Equal(20, builder.MaxPoolSize);
        Assert.Equal(300, builder.ConnectionLifetime);
    }

    [Fact]
    public void Explicit_pool_sizes_are_preserved_by_builder()
    {
        var input =
            "Host=db;Database=kasse;Username=u;Password=p;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=50;Connection Lifetime=120;";
        var builder = new NpgsqlConnectionStringBuilder(input);

        Assert.True(builder.Pooling);
        Assert.Equal(2, builder.MinPoolSize);
        Assert.Equal(50, builder.MaxPoolSize);
        Assert.Equal(120, builder.ConnectionLifetime);
    }
}
