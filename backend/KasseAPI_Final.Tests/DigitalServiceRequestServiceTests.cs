using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DigitalServiceRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_creates_pending_and_publishes_activity()
    {
        var activity = new Mock<IActivityEventPublisher>();
        activity
            .Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.DigitalServiceRequested,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (sut, db) = CreateSut(nameof(CreateAsync_creates_pending_and_publishes_activity), activity.Object);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Request Cafe",
            Slug = "request-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.CreateAsync(tenantId, "website", "please", "user-1");
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        Assert.Equal(DigitalServiceRequestStatuses.Pending, result.Request!.Status);
        Assert.Equal(TenantServiceTypes.Website, result.Request.ServiceType);

        var status = await db.TenantServiceStatuses.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId && s.ServiceType == TenantServiceTypes.Website);
        Assert.Equal(TenantDigitalServiceStatuses.Pending, status.Status);
        Assert.NotNull(status.RequestedAt);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.DigitalServiceRequested,
                It.IsAny<object?>(),
                "user-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_pending()
    {
        var (sut, db) = CreateSut(nameof(CreateAsync_rejects_duplicate_pending));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dup Cafe",
            Slug = "dup-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await sut.CreateAsync(tenantId, "app", null, "user-1");
        Assert.True(first.Succeeded);

        var second = await sut.CreateAsync(tenantId, "app", null, "user-1");
        Assert.False(second.Succeeded);
        Assert.Equal(DigitalServiceRequestService.AlreadyPendingCode, second.Code);
    }

    [Fact]
    public async Task CreateAsync_returns_not_found_for_missing_tenant()
    {
        var (sut, _) = CreateSut(nameof(CreateAsync_returns_not_found_for_missing_tenant));
        var result = await sut.CreateAsync(Guid.NewGuid(), "website", null, "user-1");
        Assert.False(result.Succeeded);
        Assert.Equal(DigitalServiceRequestService.TenantNotFoundCode, result.Code);
    }

    [Fact]
    public async Task ApproveAsync_resolves_pending()
    {
        var (sut, db) = CreateSut(nameof(ApproveAsync_resolves_pending));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Approve Cafe",
            Slug = "approve-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync(tenantId, "website", null, "mgr-1");
        Assert.True(created.Succeeded);

        var approved = await sut.ApproveAsync(created.Request!.Id, "sa-1", "ok");
        Assert.True(approved.Succeeded);
        Assert.Equal(DigitalServiceRequestStatuses.Approved, approved.Request!.Status);
        Assert.Equal("sa-1", approved.Request.ResolvedByUserId);
        Assert.Equal("ok", approved.Request.ResolutionNote);

        var status = await db.TenantServiceStatuses.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId && s.ServiceType == TenantServiceTypes.Website);
        Assert.Equal(TenantDigitalServiceStatuses.None, status.Status);
    }

    [Fact]
    public async Task RejectAsync_resolves_pending()
    {
        var (sut, db) = CreateSut(nameof(RejectAsync_resolves_pending));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Reject Cafe",
            Slug = "reject-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync(tenantId, "app", null, "mgr-1");
        var rejected = await sut.RejectAsync(created.Request!.Id, "sa-1", "later");
        Assert.True(rejected.Succeeded);
        Assert.Equal(DigitalServiceRequestStatuses.Rejected, rejected.Request!.Status);

        var status = await db.TenantServiceStatuses.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId && s.ServiceType == TenantServiceTypes.App);
        Assert.Equal(TenantDigitalServiceStatuses.Rejected, status.Status);
    }

    [Fact]
    public async Task ApproveAsync_rejects_non_pending()
    {
        var (sut, db) = CreateSut(nameof(ApproveAsync_rejects_non_pending));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Twice Cafe",
            Slug = "twice-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync(tenantId, "website", null, "mgr-1");
        await sut.ApproveAsync(created.Request!.Id, "sa-1", null);
        var again = await sut.ApproveAsync(created.Request.Id, "sa-1", null);
        Assert.False(again.Succeeded);
        Assert.Equal(DigitalServiceRequestService.InvalidStatusCode, again.Code);
    }

    [Fact]
    public async Task ListAsync_filters_by_status_and_tenant()
    {
        var (sut, db) = CreateSut(nameof(ListAsync_filters_by_status_and_tenant));
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant { Id = t1, Name = "A", Slug = "a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = t2, Name = "B", Slug = "b", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await sut.CreateAsync(t1, "website", null, "u1");
        await sut.CreateAsync(t2, "app", null, "u2");
        var created = await sut.CreateAsync(t1, "app", null, "u1");
        await sut.ApproveAsync(created.Request!.Id, "sa", null);

        var pending = await sut.ListAsync(DigitalServiceRequestStatuses.Pending);
        Assert.Equal(2, pending.Count);

        var forT1 = await sut.ListAsync(null, t1);
        Assert.Equal(2, forT1.Count);
    }

    private static (DigitalServiceRequestService Sut, AppDbContext Db) CreateSut(
        string name,
        IActivityEventPublisher? activity = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var publisher = activity ?? Mock.Of<IActivityEventPublisher>();
        var statuses = new TenantServiceStatusService(
            db,
            new DigitalServicePricingService(),
            TimeProvider.System,
            NullLogger<TenantServiceStatusService>.Instance);
        var sut = new DigitalServiceRequestService(
            db,
            statuses,
            publisher,
            TimeProvider.System,
            NullLogger<DigitalServiceRequestService>.Instance);
        return (sut, db);
    }
}
