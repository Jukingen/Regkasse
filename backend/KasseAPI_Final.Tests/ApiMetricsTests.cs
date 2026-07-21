using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MetricsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_completes_successful_request_without_throwing()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/pos/products";

        await middleware.InvokeAsync(context, new ApiRequestMetricsAccumulator());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_records_and_rethrows_exceptions()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/Auth/login";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, new ApiRequestMetricsAccumulator()));
    }

    [Fact]
    public async Task InvokeAsync_skips_metrics_endpoint()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/metrics";

        await middleware.InvokeAsync(context, new ApiRequestMetricsAccumulator());

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/api/admin/tenants/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "/api/admin/tenants/{id}")]
    [InlineData("/api/pos/cart/42/items", "/api/pos/cart/{id}/items")]
    [InlineData("", "/")]
    public void NormalizePathForMetric_reduces_cardinality(string path, string expected)
    {
        Assert.Equal(expected, MetricsMiddleware.NormalizePathForMetric(path));
    }

    [Theory]
    [InlineData("/metrics", true)]
    [InlineData("/health", true)]
    [InlineData("/api/health", true)]
    [InlineData("/api/pos/products", false)]
    public void IsExemptPath_matches_scrape_and_health(string path, bool expected)
    {
        Assert.Equal(expected, MetricsMiddleware.IsExemptPath(path));
    }

    private static MetricsMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<MetricsMiddleware>.Instance);
}

public sealed class DbQueryDurationInterceptorTests
{
    [Theory]
    [InlineData("SELECT 1", "select")]
    [InlineData("  with cte as (select 1) select * from cte", "select")]
    [InlineData("INSERT INTO t DEFAULT VALUES", "insert")]
    [InlineData("UPDATE t SET a = 1", "update")]
    [InlineData("DELETE FROM t", "delete")]
    [InlineData("BEGIN", "other")]
    [InlineData(null, "other")]
    public void ClassifyCommand_maps_sql_prefix(string? sql, string expected)
    {
        Assert.Equal(expected, DbQueryDurationInterceptor.ClassifyCommand(sql));
    }
}

public sealed class DbMetricsServiceTests
{
    [Fact]
    public async Task TrackQueryAsync_returns_factory_result()
    {
        var sut = new DbMetricsService();
        var result = await sut.TrackQueryAsync("select", async () =>
        {
            await Task.Yield();
            return 42;
        });

        Assert.Equal(42, result);
    }

    [Fact]
    public void RecordQuery_and_TrackConnection_do_not_throw()
    {
        var sut = new DbMetricsService();
        sut.RecordQuery("insert", 12.5);
        sut.TrackConnection(isOpen: true);
        sut.TrackConnection(isOpen: false);
    }
}

public sealed class CacheMetricsServiceTests
{
    [Fact]
    public void RecordHitAndMiss_updates_hit_ratio()
    {
        var sut = new CacheMetricsService();
        sut.RecordMiss();
        sut.RecordHit();
        sut.RecordHit();
        sut.RecordSize(1024);

        var ratio = sut.GetHitRatio();
        Assert.InRange(ratio, 0, 1);
    }
}

public sealed class BusinessMetricsServiceTests
{
    [Fact]
    public void Update_methods_and_RecordOrderCreated_do_not_throw()
    {
        var sut = new BusinessMetricsService();
        sut.UpdateTenantCount(3);
        sut.UpdateRevenue(199.99m);
        sut.UpdateActiveOrders(5);
        sut.RecordOrderCreated();
        sut.UpdateRegisteredUsers(12);

        Assert.Equal(3, sut.GetActiveTenants());
        Assert.Equal(5, sut.GetActiveOrders());
        Assert.Equal(12, sut.GetRegisteredUsers());
        Assert.Equal(199.99m, sut.GetRevenueEur());
    }
}

public sealed class ApiRequestMetricsAccumulatorTests
{
    [Fact]
    public void Snapshot_computes_average_and_error_rate()
    {
        var sut = new ApiRequestMetricsAccumulator();
        sut.Record(100, isError: false);
        sut.Record(300, isError: true);

        var snap = sut.Snapshot();
        Assert.Equal(2, snap.TotalRequests);
        Assert.Equal(1, snap.TotalErrors);
        Assert.Equal(200, snap.AvgResponseTimeMs);
        Assert.Equal(50, snap.ErrorRatePercent);
    }
}
