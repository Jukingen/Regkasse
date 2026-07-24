using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxBulkUpdateServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_bulk_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task UpdateTaxForProductsAsync_MovesProductsAndWritesHistory()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var oldGroupId = Guid.NewGuid();
        var newGroupId = Guid.NewGuid();
        db.TaxGroups.AddRange(
            new TaxGroup
            {
                Id = oldGroupId,
                TenantId = tenantId,
                Name = "Ermäßigt",
                Rate = 10m,
                IsActive = true,
                IsSystem = true,
                GroupType = TaxGroupType.Reduced,
                CreatedAt = DateTime.UtcNow,
            },
            new TaxGroup
            {
                Id = newGroupId,
                TenantId = tenantId,
                Name = "Normalsatz",
                Rate = 20m,
                IsActive = true,
                IsSystem = true,
                GroupType = TaxGroupType.Standard,
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

        db.Products.AddRange(
            CreateProduct(tenantId, categoryId, oldGroupId, "A", 10m),
            CreateProduct(tenantId, categoryId, oldGroupId, "B", 10m),
            CreateProduct(tenantId, categoryId, newGroupId, "C", 20m));
        await db.SaveChangesAsync();

        var sut = new TaxBulkUpdateService(db, NullLogger<TaxBulkUpdateService>.Instance);
        var actor = Guid.NewGuid();
        var result = await sut.UpdateTaxForProductsAsync(tenantId, oldGroupId, newGroupId, actor, "Bulk move");

        Assert.Equal(2, result.UpdatedProducts);
        Assert.Equal(10m, result.OldRate);
        Assert.Equal(20m, result.NewRate);
        Assert.Equal(0, await db.Products.CountAsync(p => p.TaxGroupId == oldGroupId));
        Assert.Equal(3, await db.Products.CountAsync(p => p.TaxGroupId == newGroupId));
        Assert.Equal(2, await db.TaxHistories.CountAsync());
        Assert.All(await db.TaxHistories.ToListAsync(), h =>
        {
            Assert.Equal(10m, h.OldRate);
            Assert.Equal(20m, h.NewRate);
            Assert.Equal("Bulk move", h.Reason);
            Assert.Equal(actor, h.ChangedBy);
        });
    }

    [Fact]
    public async Task UpdateTaxForProductsAsync_SameGroups_Throws()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        var groupId = Guid.NewGuid();
        var sut = new TaxBulkUpdateService(db, NullLogger<TaxBulkUpdateService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateTaxForProductsAsync(tenantId, groupId, groupId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ApplyTaxGroupToProductsAsync_UpdatesSelectionAndWritesHistory()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var oldGroupId = Guid.NewGuid();
        var newGroupId = Guid.NewGuid();
        db.TaxGroups.AddRange(
            new TaxGroup
            {
                Id = oldGroupId,
                TenantId = tenantId,
                Name = "Ermäßigt",
                Rate = 10m,
                IsActive = true,
                GroupType = TaxGroupType.Reduced,
                CreatedAt = DateTime.UtcNow,
            },
            new TaxGroup
            {
                Id = newGroupId,
                TenantId = tenantId,
                Name = "Normalsatz",
                Rate = 20m,
                IsActive = true,
                GroupType = TaxGroupType.Standard,
                CreatedAt = DateTime.UtcNow,
            });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "c1",
            Name = "Cat",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var p1 = CreateProduct(tenantId, categoryId, oldGroupId, "A", 10m);
        var p2 = CreateProduct(tenantId, categoryId, newGroupId, "B", 20m);
        db.Products.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var sut = new TaxBulkUpdateService(db, NullLogger<TaxBulkUpdateService>.Instance);
        var result = await sut.ApplyTaxGroupToProductsAsync(
            tenantId,
            newGroupId,
            [p1.Id, p2.Id, Guid.NewGuid()],
            Guid.NewGuid(),
            reason: "Quick tax assign test");

        Assert.Equal(3, result.RequestedCount);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Equal(1, result.UnchangedProducts);
        Assert.Equal(1, result.NotFound);
        Assert.Equal(20m, result.NewRate);

        var updated = await db.Products.SingleAsync(p => p.Id == p1.Id);
        Assert.Equal(newGroupId, updated.TaxGroupId);
        Assert.Equal(20m, updated.TaxRate);
        Assert.Equal(1, await db.TaxHistories.CountAsync(h => h.ProductId == p1.Id));
    }

    private static Product CreateProduct(
        Guid tenantId,
        Guid categoryId,
        Guid taxGroupId,
        string name,
        decimal taxRate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = name,
            Category = "Test",
            Price = 5m,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = $"BC-{name}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
}
