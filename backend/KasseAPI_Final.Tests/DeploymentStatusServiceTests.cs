using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Deployment;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeploymentStatusServiceTests
{
    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"Deployments_{Guid.NewGuid():N}";
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

    private static DeploymentStatusService CreateService(IDbContextFactory<AppDbContext> factory) =>
        new(factory, NullLogger<DeploymentStatusService>.Instance);

    [Fact]
    public async Task ReportAsync_UpsertsByRunUrlAndStage()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory);

        var first = await svc.ReportAsync(new DeploymentCiReportRequest
        {
            Stage = "canary",
            Status = "deploying",
            GitSha = "abc123",
            ImageTag = "ghcr.io/x/api:sha-abc1234",
            RunUrl = "https://github.com/org/repo/actions/runs/1",
            TenantIds = new[] { "demo", "canary" },
            TriggeredBy = "ci",
        });

        Assert.Equal("canary", first.Stage);
        Assert.Equal("deploying", first.Status);
        Assert.Equal(2, first.TenantIds.Count);

        var second = await svc.ReportAsync(new DeploymentCiReportRequest
        {
            Stage = "canary",
            Status = "succeeded",
            GitSha = "abc123",
            ImageTag = "ghcr.io/x/api:sha-abc1234",
            RunUrl = "https://github.com/org/repo/actions/runs/1",
            TenantIds = new[] { "demo", "canary" },
            TriggeredBy = "ci",
        });

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("succeeded", second.Status);

        var list = await svc.ListAsync(stage: "canary");
        Assert.Equal(1, list.Total);
        Assert.NotNull(list.LatestByStage["canary"]);
        Assert.Equal("succeeded", list.LatestByStage["canary"]!.Status);
    }

    [Fact]
    public async Task ReportAsync_RejectsInvalidStage()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.ReportAsync(new DeploymentCiReportRequest
        {
            Stage = "lab",
            Status = "succeeded",
        }));
    }
}
