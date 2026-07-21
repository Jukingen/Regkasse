using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task CreateSubscriptionAsync_fails_when_service_unknown()
    {
        var (sut, _) = CreateSut(nameof(CreateSubscriptionAsync_fails_when_service_unknown));
        var result = await sut.CreateSubscriptionAsync(Guid.NewGuid(), "no-such-service");
        Assert.False(result.Succeeded);
        Assert.Equal(SubscriptionService.ServiceNotFoundCode, result.Code);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_fails_when_tenant_missing()
    {
        var (sut, _) = CreateSut(nameof(CreateSubscriptionAsync_fails_when_tenant_missing));
        var result = await sut.CreateSubscriptionAsync(Guid.NewGuid(), "website-starter");
        Assert.False(result.Succeeded);
        Assert.Equal(SubscriptionService.TenantNotFoundCode, result.Code);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_persists_active_subscription()
    {
        var tenantId = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(CreateSubscriptionAsync_persists_active_subscription));
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Sub Tenant",
            Slug = "sub-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var actor = Guid.NewGuid().ToString("D");
        var result = await sut.CreateSubscriptionAsync(tenantId, "website-starter", actor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Subscription);
        Assert.Equal(SubscriptionStatuses.Active, result.Subscription.Status);
        Assert.Equal(99m, result.Subscription.Price);
        Assert.Equal("website-starter", result.Subscription.ServiceId);
        Assert.True(result.Subscription.NextBillingDate > result.Subscription.CreatedAt);

        var stored = await db.Subscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == result.Subscription.Id);
        Assert.Equal(tenantId, stored.TenantId);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_rejects_duplicate_active()
    {
        var tenantId = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(CreateSubscriptionAsync_rejects_duplicate_active));
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dup Tenant",
            Slug = "dup-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var first = await sut.CreateSubscriptionAsync(tenantId, "app-pwa");
        Assert.True(first.Succeeded);

        var second = await sut.CreateSubscriptionAsync(tenantId, "app-pwa");
        Assert.False(second.Succeeded);
        Assert.Equal(SubscriptionService.AlreadyActiveCode, second.Code);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_marks_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(CancelSubscriptionAsync_marks_cancelled));
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cancel Tenant",
            Slug = "cancel-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var created = await sut.CreateSubscriptionAsync(tenantId, "app-native");
        Assert.True(created.Succeeded);

        var cancelled = await sut.CancelSubscriptionAsync(created.Subscription!.Id, Guid.NewGuid().ToString("D"));
        Assert.True(cancelled.Succeeded);
        Assert.Equal(SubscriptionStatuses.Cancelled, cancelled.Subscription!.Status);
        Assert.NotNull(cancelled.Subscription.CancelledAtUtc);
    }

    [Fact]
    public async Task GetDigitalBillingDashboardAsync_aggregates_active_mrr_by_type()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (sut, db) = CreateSut(nameof(GetDigitalBillingDashboardAsync_aggregates_active_mrr_by_type));
        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantA,
                Name = "Cafe A",
                Slug = "cafe-a",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = tenantB,
                Name = "Cafe B",
                Slug = "cafe-b",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        Assert.True((await sut.CreateSubscriptionAsync(tenantA, "website-starter")).Succeeded);
        Assert.True((await sut.CreateSubscriptionAsync(tenantA, "app-pwa")).Succeeded);
        Assert.True((await sut.CreateSubscriptionAsync(tenantB, "website-professional")).Succeeded);

        var cancelled = await sut.CreateSubscriptionAsync(tenantB, "app-native");
        Assert.True(cancelled.Succeeded);
        Assert.True((await sut.CancelSubscriptionAsync(cancelled.Subscription!.Id)).Succeeded);

        var dashboard = await sut.GetDigitalBillingDashboardAsync();

        Assert.Equal(99m + 199m + 299m, dashboard.Total);
        Assert.Equal(99m + 299m, dashboard.Websites);
        Assert.Equal(199m, dashboard.Apps);
        Assert.Equal(3, dashboard.Subscribers);
        Assert.Equal(4, dashboard.Subscriptions.Count);
        Assert.Contains(dashboard.Subscriptions, r => r.Tenant == "Cafe A" && r.ServiceId == "website-starter");
        Assert.Contains(dashboard.Subscriptions, r => r.Status == SubscriptionStatuses.Cancelled);
    }

    private static (SubscriptionService Sut, AppDbContext Db) CreateSut(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var audit = new Mock<IBillingAuditService>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new SubscriptionService(
            db,
            audit.Object,
            TimeProvider.System,
            NullLogger<SubscriptionService>.Instance);

        return (sut, db);
    }
}
