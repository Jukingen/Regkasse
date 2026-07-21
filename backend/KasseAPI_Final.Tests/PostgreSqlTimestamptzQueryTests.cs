using KasseAPI_Final.Data;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Ensures EF + Npgsql accept query parameters only when <see cref="DateTimeKind"/> is UTC (regression for timestamptz).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlTimestamptzQueryTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public PostgreSqlTimestamptzQueryTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    [SkippableFact]
    public async Task PaymentDetails_Count_NormalizedUnspecifiedBounds_DoesNotThrow()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = CreateContext();

        var rawStart = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Unspecified);
        var rawEnd = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Unspecified);

        var fromUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(rawStart);
        var toExclusiveUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(rawEnd);

        Assert.Equal(DateTimeKind.Utc, fromUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, toExclusiveUtc.Kind);

        var count = await ctx.PaymentDetails.AsNoTracking()
            .CountAsync(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc);

        Assert.True(count >= 0);
    }
}
