using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseApiGatewayServiceTests
{
    [Fact]
    public async Task ConfigureGatewayAsync_PersistsStrategyAndEndpoints()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var config = await svc.ConfigureGatewayAsync(new ConfigureTseGatewayRequestDto
        {
            Strategy = "Weighted",
            HealthCheckInterval = 45,
            Timeout = 3000,
            RetryCount = 2,
            Enabled = true,
            Endpoints =
            {
                new ConfigureTseGatewayEndpointRequestDto
                {
                    Provider = "fake",
                    Endpoint = "local://fake-a",
                    Weight = 3,
                    Enabled = true,
                },
                new ConfigureTseGatewayEndpointRequestDto
                {
                    Provider = "soft",
                    Endpoint = "local://soft-a",
                    Weight = 1,
                    Enabled = true,
                },
            },
        }, "admin");

        Assert.Equal(TseLoadBalancingStrategies.Weighted, config.Strategy);
        Assert.Equal(45, config.HealthCheckInterval);
        Assert.Equal(3000, config.Timeout);
        Assert.Equal(2, config.Endpoints.Count);
        Assert.Equal(2, await db.TseGatewayEndpoints.CountAsync());
    }

    [Fact]
    public async Task RouteRequestAsync_RejectsFiscalSignOperation()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.RouteRequestAsync(new TseGatewayRequestDto
        {
            Operation = "Sign",
        });

        Assert.False(result.Success);
        Assert.Contains("HealthProbe", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.SimulationOnly);
    }

    [Fact]
    public async Task RouteRequestAsync_HealthProbe_SucceedsViaFakeProvider()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.ConfigureGatewayAsync(new ConfigureTseGatewayRequestDto
        {
            Strategy = "RoundRobin",
            RetryCount = 1,
            Timeout = 2000,
            Endpoints =
            {
                new ConfigureTseGatewayEndpointRequestDto
                {
                    Provider = "fake",
                    Endpoint = "local://fake",
                    Enabled = true,
                    Weight = 1,
                },
            },
        });

        var result = await svc.RouteRequestAsync(new TseGatewayRequestDto
        {
            Operation = TseGatewayOperations.HealthProbe,
            CorrelationId = "gw-test-1",
        });

        Assert.True(result.Success);
        Assert.Equal("fake", result.SelectedProvider);
        Assert.Equal(1, result.Attempts);

        var status = await svc.GetGatewayStatusAsync();
        Assert.True(status.Stats.TotalRequests >= 1);
        Assert.Equal(100, status.Stats.SuccessRate);
        Assert.Contains(status.Endpoints, e => e.Status == "healthy");
    }

    [Fact]
    public async Task RouteRequestAsync_RetriesUnhealthyThenSucceeds()
    {
        await using var db = CreateDb();
        var ready = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["epson"] = false,
            ["fake"] = true,
        };
        var svc = CreateService(db, ready);

        await svc.ConfigureGatewayAsync(new ConfigureTseGatewayRequestDto
        {
            Strategy = "RoundRobin",
            RetryCount = 2,
            Timeout = 2000,
            Endpoints =
            {
                new ConfigureTseGatewayEndpointRequestDto
                {
                    Provider = "epson",
                    Endpoint = "https://epson.example",
                    Enabled = true,
                    SortOrder = 0,
                },
                new ConfigureTseGatewayEndpointRequestDto
                {
                    Provider = "fake",
                    Endpoint = "local://fake",
                    Enabled = true,
                    SortOrder = 1,
                },
            },
        });

        var result = await svc.RouteRequestAsync(new TseGatewayRequestDto
        {
            Operation = TseGatewayOperations.HealthProbe,
        });

        Assert.True(result.Success);
        Assert.Equal("fake", result.SelectedProvider);
        Assert.True(result.Attempts >= 2);
    }

    private static TseApiGatewayService CreateService(
        AppDbContext db,
        Dictionary<string, bool>? readyByProvider = null)
    {
        readyByProvider ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = true,
            ["soft"] = true,
            ["fiskaly"] = true,
            ["epson"] = false,
            ["swissbit"] = false,
        };

        var factory = new Mock<ITseProviderFactory>();
        factory.Setup(f => f.GetKnownProviderNames()).Returns(new[]
        {
            "fiskaly", "epson", "swissbit", "fake", "soft",
        });
        factory.Setup(f => f.IsProviderConfigured(It.IsAny<string>()))
            .Returns((string n) => string.Equals(n, "fake", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "soft", StringComparison.OrdinalIgnoreCase));
        factory.Setup(f => f.GetProvider(It.IsAny<string>()))
            .Returns((string name) =>
            {
                var ready = readyByProvider.TryGetValue(name, out var r) && r;
                var provider = new Mock<ITseProvider>();
                provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ready);
                return provider.Object;
            });

        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new TseOptions());

        return new TseApiGatewayService(
            db,
            factory.Object,
            new TseGatewayMetricsStore(),
            monitor.Object,
            NullLogger<TseApiGatewayService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_gateway_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }
}
