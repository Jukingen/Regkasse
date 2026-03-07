using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Modifier migration service – run creates product+link; idempotent second run skips; dry-run no writes; invalid category errors.
/// </summary>
public class ModifierMigrationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModifierMigration_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task MigrateAsync_ValidCategoryAndOneModifier_CreatesProductAndLink()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: false);

        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(1, result.MigratedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.Migrated);
        Assert.Equal(modifierId, result.Migrated[0].ModifierId);
        Assert.NotNull(result.Migrated[0].ProductId);

        var product = await context.Products.FindAsync(result.Migrated[0].ProductId);
        Assert.NotNull(product);
        Assert.True(product.IsSellableAddOn);
        Assert.Equal("Ketchup", product.Name);
        Assert.Equal(0.30m, product.Price);

        var link = await context.AddOnGroupProducts.FirstOrDefaultAsync(a => a.ProductId == product!.Id && a.ModifierGroupId == groupId);
        Assert.NotNull(link);
    }

    [Fact]
    public async Task MigrateAsync_SecondRun_SkipsAlreadyMigrated()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var first = await service.MigrateAsync(categoryId, dryRun: false);
        Assert.Equal(1, first.MigratedCount);

        var second = await service.MigrateAsync(categoryId, dryRun: false);
        Assert.Equal(1, second.TotalProcessed);
        Assert.Equal(0, second.MigratedCount);
        Assert.Equal(1, second.SkippedCount);
        Assert.Single(second.Skipped);
        Assert.Equal(modifierId, second.Skipped[0].ModifierId);

        // Idempotency: one add-on product in group with same name+price (no legacy_modifier_id).
        var productCount = await context.AddOnGroupProducts
            .Where(a => a.ModifierGroupId == groupId)
            .Where(a => a.Product.Name == "Ketchup" && a.Product.Price == 0.30m)
            .CountAsync();
        Assert.Equal(1, productCount);
    }

    [Fact]
    public async Task MigrateAsync_DryRun_DoesNotCreateProducts()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: true);

        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(1, result.MigratedCount);
        Assert.Equal(0, result.SkippedCount);

        var product = await context.Products.FirstOrDefaultAsync(p => p.IsSellableAddOn && p.Name == "Ketchup");
        Assert.Null(product);
        Assert.Empty(context.AddOnGroupProducts);
    }

    [Fact]
    public async Task MigrateAsync_InvalidCategory_ReturnsError()
    {
        await using var context = CreateContext();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var badCategoryId = Guid.NewGuid();

        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(badCategoryId, dryRun: false);

        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Contains("not found", result.Errors[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.MigratedCount);
    }

    [Fact]
    public async Task MigrateAsync_ModifierWithInactiveGroup_AddsToErrors()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = false });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: false);

        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Contains("inactive", result.Errors[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.MigratedCount);
    }

    /// <summary>Risk: Two modifiers with same name in same group both get migrated (duplicate name allowed; no conflict).</summary>
    [Fact]
    public async Task MigrateAsync_TwoModifiersSameName_BothMigratedAsSeparateProducts()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifier1Id = Guid.NewGuid();
        var modifier2Id = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifier1Id,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifier2Id,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.50m,
            TaxType = 2,
            SortOrder = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: false);

        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.MigratedCount);
        Assert.Equal(0, result.ErrorCount);
        var products = await context.AddOnGroupProducts
            .Where(a => a.ModifierGroupId == groupId)
            .Select(a => a.Product)
            .Distinct()
            .ToListAsync();
        Assert.Equal(2, products.Count);
        Assert.Equal(2, products.Select(p => p.Name).Count(n => n == "Ketchup"));
        Assert.Contains(products, p => p.Price == 0.30m);
        Assert.Contains(products, p => p.Price == 0.50m);
    }
}
