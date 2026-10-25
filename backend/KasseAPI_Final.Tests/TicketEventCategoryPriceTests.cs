using System.Runtime.CompilerServices;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TicketEventCategoryPriceTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();
    [Fact]
    public async Task SetCategoryPrice_UpsertsPriceAndCapacity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent(), CancellationToken.None);
        var category = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                tenantId,
                new UpsertTicketPriceCategoryRequest { Code = "student", Name = "Student" },
                "manager-1",
                CancellationToken.None));

        var first = Assert.IsType<TicketCatalogResult.PriceOk>(
            await events.SetCategoryPriceAsync(
                created!.Id,
                new SetTicketEventCategoryPriceRequest { CategoryId = category.Category.Id, Price = 12.5m, Capacity = 8 },
                "manager-1",
                CancellationToken.None));
        var second = Assert.IsType<TicketCatalogResult.PriceOk>(
            await events.SetCategoryPriceAsync(
                created.Id,
                new SetTicketEventCategoryPriceRequest { CategoryId = category.Category.Id, Price = 15m, Capacity = 6 },
                "manager-1",
                CancellationToken.None));

        Assert.Equal(first.Price.Id, second.Price.Id);
        Assert.Equal(15m, second.Price.Price);
        Assert.Equal(6, second.Price.Capacity);
        Assert.Equal(1, await db.TicketEventCategoryPrices.CountAsync());
    }

    [Fact]
    public async Task SetCategoryPrice_UnknownCategory_Returns400_ForeignEvent_Returns404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = await CreateDbAsync(owner);
        SeedTenant(db, owner);
        SeedTenant(db, other);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(owner, SampleEvent(), CancellationToken.None);
        var missingCategory = await events.SetCategoryPriceAsync(
            created!.Id,
            new SetTicketEventCategoryPriceRequest { CategoryId = Guid.NewGuid(), Price = 10m, Capacity = 1 },
            "manager-1",
            CancellationToken.None);
        var bad = Assert.IsType<TicketCatalogResult.Error>(missingCategory);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.InvalidCategory, bad.Code);

        var accessor = (TenantTestDoubles.MutableTenantAccessor)Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("missing accessor"));
        accessor.TenantId = other;

        var foreign = await events.SetCategoryPriceAsync(
            created.Id,
            new SetTicketEventCategoryPriceRequest { CategoryId = Guid.NewGuid(), Price = 10m, Capacity = 1 },
            "manager-1",
            CancellationToken.None);
        Assert.IsType<TicketCatalogResult.NotFound>(foreign);
    }

    [Fact]
    public async Task AddSeat_CategoryWithoutPriceRow_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = await events.CreateAsync(tenantId, SampleEvent(), CancellationToken.None);
        var category = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                tenantId,
                new UpsertTicketPriceCategoryRequest { Code = "vip", Name = "VIP" },
                "manager-1",
                CancellationToken.None));
        var result = await events.AddSeatAsync(
            created!.Id,
            new CreateTicketSeatRequest { Row = "A", Number = "1", CategoryId = category.Category.Id },
            CancellationToken.None);
        var error = Assert.IsType<TicketSeatActionResult.Error>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(TicketSeatErrorCodes.CategoryPriceMissing, error.Code);
    }

    private static UpsertTicketEventRequest SampleEvent() => new()
    {
        Name = "Abend",
        Venue = "Saal",
        Capacity = 20,
        IsActive = true,
        StartAtUtc = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc),
        EndAtUtc = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
    };

    private static TicketEventService CreateEvents(AppDbContext db) =>
        new(db, TicketAuditTestDoubles.Loose().Object, NullLogger<TicketEventService>.Instance);

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TicketPrices_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
    }
}
