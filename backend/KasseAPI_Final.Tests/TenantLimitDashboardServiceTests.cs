using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantLimitDashboardServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLimitDashboard_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task GetDashboardAsync_IncludesAllLimitRowsAndCriticalUser()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Limits cafe",
            Slug = "limits-dash",
            IsActive = true,
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        var caps = TenantLimits.CreateDefault(TenantId);
        caps.MaxActiveRegistersPerUser = 1;
        caps.MaxProductsPerTenant = 10;
        db.TenantLimits.Add(caps);
        db.Users.Add(new ApplicationUser
        {
            Id = "cashier-1",
            UserName = "cashier1",
            FirstName = "Anna",
            LastName = "Kassier",
            Email = "anna@example.com",
            Role = Roles.Cashier,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RegisterNumber = "K-1",
            Location = "Wien",
            Status = RegisterStatus.Open,
            AssignedUserId = "cashier-1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = "Coffee",
            Price = 1m,
            Category = "C",
            CategoryId = Guid.NewGuid(),
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = 10m,
            Barcode = $"bc-{Guid.NewGuid():N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.ActivityEvents.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Type = ActivityEventType.LimitExceeded,
            Severity = ActivitySeverityNames.Error,
            Title = "Limit exceeded",
            Description = "Products full",
            EntityType = "tenant_limit",
            EntityId = TenantLimitKeys.MaxProductsPerTenant,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var cache = new TenantLimitCacheService(
            new MemoryCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MemoryCacheService>.Instance,
                new CacheMetricsService()),
            Options.Create(new KasseAPI_Final.Configuration.CacheSettings()),
            NullLogger<TenantLimitCacheService>.Instance);
        var limits = new TenantLimitService(db, cache, NullLogger<TenantLimitService>.Instance);
        var guard = new TenantLimitGuard(db, limits, NullLogger<TenantLimitGuard>.Instance);
        var sut = new TenantLimitDashboardService(db, guard);

        var dto = await sut.GetDashboardAsync(TenantId, readerUserId: "mgr-1");

        Assert.False(dto.AllTenants);
        Assert.Equal(9, dto.Limits.Count);
        Assert.Equal(9, dto.Summary.Total);
        Assert.Contains(dto.Limits, l => l.Key == TenantLimitKeys.MaxProductsPerTenant && l.Current == 1);
        var products = Assert.Single(dto.Limits, l => l.Key == TenantLimitKeys.MaxProductsPerTenant);
        Assert.Equal("limits-dash", products.TenantSlug);
        Assert.Equal(LimitUsageStatuses.Increasing, products.Trend);
        Assert.True(products.ChangeCount >= 1);
        var critical = Assert.Single(dto.CriticalUsers);
        Assert.Equal("cashier-1", critical.UserId);
        Assert.Equal("Anna Kassier", critical.DisplayName);
        Assert.Equal(Roles.Cashier, critical.Role);
        Assert.Equal(LimitUsageStatuses.Full, critical.Status);
        Assert.False(string.IsNullOrWhiteSpace(critical.RecommendedAction));
        Assert.Single(dto.RecentActivity);
        Assert.Equal(LimitUsageStatuses.Critical, dto.RecentActivity[0].Status);
        Assert.Equal(1, dto.UnreadAlertCount);
        Assert.True(dto.TotalViolations >= 1);
        Assert.True(dto.LastUpdated <= DateTime.UtcNow.AddMinutes(1));
    }
}
