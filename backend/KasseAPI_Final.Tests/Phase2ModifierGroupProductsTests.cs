using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Add-on product group loading. Groups with AddOnGroupProducts return Products (primary); legacy Modifiers still loaded for compat.
/// </summary>
public class Phase2ModifierGroupProductsTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModifierGroupProducts_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ModifierGroup_WithAddOnGroupProducts_LoadsProductsWithPriceAndName()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Extra Käse",
            Price = 1.50m,
            CategoryId = categoryId,
            Category = "Extras",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true,
            IsSellableAddOn = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            Name = "Extras",
            SortOrder = 0,
            IsActive = true
        });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct
        {
            ModifierGroupId = groupId,
            ProductId = productId,
            SortOrder = 0
        });
        await context.SaveChangesAsync();

        var groups = await context.ProductModifierGroups
            .Where(g => g.IsActive)
            .Include(g => g.Modifiers.Where(m => m.IsActive))
            .Include(g => g.AddOnGroupProducts)
            .ThenInclude(a => a.Product)
            .OrderBy(g => g.SortOrder)
            .ToListAsync();

        Assert.Single(groups);
        var group = groups[0];
        Assert.Single(group.AddOnGroupProducts);
        var link = group.AddOnGroupProducts.First();
        Assert.NotNull(link.Product);
        Assert.Equal("Extra Käse", link.Product.Name);
        Assert.Equal(1.50m, link.Product.Price);
        Assert.True(link.Product.IsSellableAddOn);
    }

    [Fact]
    public async Task ModifierGroup_WithLegacyModifiersAndProducts_LoadsBoth()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Add-on Product",
            Price = 2.00m,
            CategoryId = categoryId,
            Category = "Extras",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true,
            IsSellableAddOn = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            Name = "Extras",
            SortOrder = 0,
            IsActive = true
        });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Legacy Ketchup",
            Price = 0.30m,
            TaxType = 2,
            IsActive = true
        });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct
        {
            ModifierGroupId = groupId,
            ProductId = productId,
            SortOrder = 1
        });
        await context.SaveChangesAsync();

        var groups = await context.ProductModifierGroups
            .Where(g => g.IsActive)
            .Include(g => g.Modifiers.Where(m => m.IsActive))
            .Include(g => g.AddOnGroupProducts)
            .ThenInclude(a => a.Product)
            .ToListAsync();

        Assert.Single(groups);
        var group = groups[0];
        Assert.Single(group.Modifiers);
        Assert.Equal("Legacy Ketchup", group.Modifiers.First().Name);
        Assert.Single(group.AddOnGroupProducts);
        Assert.Equal("Add-on Product", group.AddOnGroupProducts.First().Product!.Name);
    }
}
