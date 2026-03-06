using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for modifier-product assignment and DB-backed price lookup (RKSV/fiscal).
/// </summary>
public class ProductModifierValidationServiceTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModifierValidation_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAllowedModifierIdsForProduct_WhenAssigned_ReturnsModifierId()
    {
        await using var context = CreateInMemoryContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Getränke", VatRate = 20m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Cola",
            Price = 2.50m,
            CategoryId = categoryId,
            Category = "Getränke",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 1,
            IsActive = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0 });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productId,
            ModifierGroupId = groupId,
            SortOrder = 0
        });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 1
        });
        await context.SaveChangesAsync();

        var svc = new ProductModifierValidationService(context);
        var allowed = await svc.GetAllowedModifierIdsForProductAsync(productId);

        Assert.Contains(modifierId, allowed);
        Assert.Single(allowed);
    }

    [Fact]
    public async Task GetAllowedModifierIdsForProduct_WhenNotAssigned_ReturnsEmpty()
    {
        await using var context = CreateInMemoryContext();
        var productId = Guid.NewGuid();
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Water",
            Price = 1.00m,
            CategoryId = Guid.NewGuid(),
            Category = "Getränke",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var svc = new ProductModifierValidationService(context);
        var allowed = await svc.GetAllowedModifierIdsForProductAsync(productId);

        Assert.Empty(allowed);
    }

    [Fact]
    public async Task GetAllowedModifiersWithPricesForProduct_ReturnsDbPrice()
    {
        await using var context = CreateInMemoryContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0 });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productId,
            ModifierGroupId = groupId,
            SortOrder = 0
        });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2
        });
        await context.SaveChangesAsync();

        var svc = new ProductModifierValidationService(context);
        var list = await svc.GetAllowedModifiersWithPricesForProductAsync(productId, new[] { modifierId });

        Assert.Single(list);
        Assert.Equal(modifierId, list[0].Id);
        Assert.Equal("Ketchup", list[0].Name);
        Assert.Equal(0.30m, list[0].Price);
    }
}
