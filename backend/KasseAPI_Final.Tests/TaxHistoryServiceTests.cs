using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxHistoryServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_history_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task RecordChangeAsync_PersistsAndListsWithProductName()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Normalsatz",
            Rate = 20m,
            IsActive = true,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "test",
            Name = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = "Espresso",
            Category = "Test",
            Price = 3.5m,
            TaxRate = 20m,
            TaxType = TaxTypes.Standard,
            Unit = "pcs",
            Barcode = "ESP-1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new TaxHistoryService(db, NullLogger<TaxHistoryService>.Instance);
        var actor = Guid.NewGuid();
        await sut.RecordChangeAsync(tenantId, productId, taxGroupId, 10m, 20m, actor, "Manual correction");

        var history = await sut.GetHistoryAsync(tenantId);
        Assert.Single(history);
        Assert.Equal("Espresso", history[0].ProductName);
        Assert.Equal(10m, history[0].OldRate);
        Assert.Equal(20m, history[0].NewRate);
        Assert.Equal("Manual correction", history[0].Reason);
        Assert.Equal(actor, history[0].ChangedBy);
    }
}
