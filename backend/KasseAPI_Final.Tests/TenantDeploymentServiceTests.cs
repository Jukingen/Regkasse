using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantDeploymentServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"TenantDeploy_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        return (factory.Object, dbName);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static async Task SeedTenantsAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Tenants.AddRange(
            new Tenant { Id = TenantA, Name = "Cafe One", Slug = "cafe-1", IsActive = true },
            new Tenant { Id = TenantB, Name = "Cafe Two", Slug = "cafe-2", IsActive = true });
        await db.SaveChangesAsync();
    }

    private static TenantDeploymentService CreateService(
        IDbContextFactory<AppDbContext> factory,
        DeploymentOptions? options = null,
        IHttpClientFactory? httpFactory = null)
    {
        options ??= new DeploymentOptions
        {
            CanaryDefaultSoakHours = 24,
            CanaryTenantSlugs = ["cafe-1", "cafe-2"],
            RollbackWebhooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["canary"] = "https://example.test/rollback",
            },
        };

        httpFactory ??= CreateHttpFactory(HttpStatusCode.OK);
        return new TenantDeploymentService(
            factory,
            Options.Create(options),
            httpFactory,
            NullLogger<TenantDeploymentService>.Instance);
    }

    private static IHttpClientFactory CreateHttpFactory(HttpStatusCode status)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status));

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("deployment-rollback")).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task RecordAsync_CanarySucceeded_SetsSoakWindow()
    {
        var (factory, _) = CreateFactory();
        await SeedTenantsAsync(factory);
        var svc = CreateService(factory);

        var dto = await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-1",
            Version = "ghcr.io/x/api:sha-abc1234",
            Stage = "canary",
            Status = "succeeded",
            SmokePassed = true,
            SoakHours = 48,
        });

        Assert.Equal("canary_soak", dto.Status);
        Assert.True(dto.IsCanarySoaking);
        Assert.NotNull(dto.SoakUntilUtc);
        Assert.True(dto.SoakUntilUtc > DateTime.UtcNow.AddHours(40));
    }

    [Fact]
    public async Task ListLatestPerTenantAsync_ReturnsNewestPerTenant()
    {
        var (factory, _) = CreateFactory();
        await SeedTenantsAsync(factory);
        var svc = CreateService(factory);

        await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-1",
            Version = "v1",
            Stage = "canary",
            Status = "succeeded",
        });
        await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-1",
            Version = "v2",
            Stage = "canary",
            Status = "succeeded",
        });
        await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-2",
            Version = "v2",
            Stage = "canary",
            Status = "failed",
            ErrorMessage = "smoke",
        });

        var list = await svc.ListLatestPerTenantAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal("v2", list.Single(t => t.TenantSlug == "cafe-1").Version);
        Assert.Equal("failed", list.Single(t => t.TenantSlug == "cafe-2").Status);
    }

    [Fact]
    public async Task GetOverallStatusAsync_RecommendsNextConfiguredSlug()
    {
        var (factory, _) = CreateFactory();
        await SeedTenantsAsync(factory);
        var svc = CreateService(factory);

        await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-1",
            Version = "v1",
            Stage = "canary",
            Status = "succeeded",
        });

        var status = await svc.GetOverallStatusAsync();
        Assert.Equal(1, status.CanarySoakingCount);
        Assert.Equal("cafe-2", status.RecommendedNextCanaryTenantSlug);
        Assert.Equal("docs/CANARY_DEPLOYMENT.md", status.StrategyDoc);
    }

    [Fact]
    public async Task RollbackTenantAsync_InvokesWebhookAndRecordsHistory()
    {
        var (factory, _) = CreateFactory();
        await SeedTenantsAsync(factory);
        var svc = CreateService(factory);

        await svc.RecordAsync(new TenantDeploymentRecordRequest
        {
            TenantIdOrSlug = "cafe-1",
            Version = "v2",
            PreviousVersion = "v1",
            Stage = "canary",
            Status = "succeeded",
        });

        var result = await svc.RollbackTenantAsync(
            TenantA,
            new TenantDeploymentRollbackRequest { Confirm = "rollback" },
            "tester");

        Assert.True(result.Invoked);
        Assert.Equal("v1", result.PreviousImageTag);

        var latest = await svc.GetLatestForTenantAsync(TenantA);
        Assert.NotNull(latest);
        Assert.Equal("rolled_back", latest!.Status);
        Assert.Equal("v1", latest.Version);
    }

    [Fact]
    public async Task RollbackTenantAsync_RequiresConfirmPhrase()
    {
        var (factory, _) = CreateFactory();
        await SeedTenantsAsync(factory);
        var svc = CreateService(factory);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RollbackTenantAsync(
                TenantA,
                new TenantDeploymentRollbackRequest { Confirm = "yes" },
                "tester"));
    }
}
