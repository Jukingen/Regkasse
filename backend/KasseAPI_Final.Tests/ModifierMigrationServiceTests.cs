using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
        Assert.NotNull(product.Description);
        Assert.Equal("Ketchup", product.Description);

        var link = await context.AddOnGroupProducts.FirstOrDefaultAsync(a => a.ProductId == product!.Id && a.ModifierGroupId == groupId);
        Assert.NotNull(link);
    }

    /// <summary>Production-safe: products.description NOT NULL in some DBs. Migration must always set it.</summary>
    [Fact]
    public async Task MigrateAsync_CreatedProduct_HasDescriptionNeverNull()
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
            Name = "Extra Käse",
            Price = 1.50m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: false);

        Assert.Equal(1, result.MigratedCount);
        var product = await context.Products.FindAsync(result.Migrated[0].ProductId);
        Assert.NotNull(product);
        Assert.NotNull(product.Description);
        Assert.Equal("Extra Käse", product.Description);
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

    /// <summary>Best-effort batch: when one item fails (e.g. inactive group), previous successes persist; failed modifier remains active.</summary>
    [Fact]
    public async Task MigrateAsync_WhenOneSucceedsAndOneFails_PartialSuccessPersistedAndFailedModifierRemainsActive()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupActiveId = Guid.NewGuid();
        var groupInactiveId = Guid.NewGuid();
        var modifierSuccessId = Guid.NewGuid();
        var modifierFailId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupActiveId, Name = "Active", SortOrder = 0, IsActive = true });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupInactiveId, Name = "Inactive", SortOrder = 1, IsActive = false });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierSuccessId,
            ModifierGroupId = groupActiveId,
            Name = "Mayo",
            Price = 0.50m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierFailId,
            ModifierGroupId = groupInactiveId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateAsync(categoryId, dryRun: false);

        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.MigratedCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Migrated);
        Assert.Equal(modifierSuccessId, result.Migrated[0].ModifierId);
        Assert.Single(result.Errors);
        Assert.Equal(modifierFailId, result.Errors[0].ModifierId);
        Assert.Contains("inactive", result.Errors[0].Reason, StringComparison.OrdinalIgnoreCase);

        var productForSuccess = await context.Products.FindAsync(result.Migrated[0].ProductId);
        Assert.NotNull(productForSuccess);
        Assert.True(productForSuccess.IsSellableAddOn);
        Assert.Equal("Mayo", productForSuccess.Name);

        var failedModifier = await context.ProductModifiers.FindAsync(modifierFailId);
        Assert.NotNull(failedModifier);
        Assert.True(failedModifier.IsActive);
        var productForFailed = await context.AddOnGroupProducts
            .Where(a => a.ModifierGroupId == groupInactiveId)
            .Select(a => a.Product)
            .FirstOrDefaultAsync();
        Assert.Null(productForFailed);
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

    // ========== MigrateSingleByModifierIdAsync (Admin POST /api/admin/modifiers/{id}/migrate-to-product) ==========

    [Fact]
    public async Task MigrateSingleByModifierId_ValidModifier_CreatesProductAndMarksModifierInactive()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Mayo",
            Price = 0.50m,
            TaxType = 2,
            SortOrder = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateSingleByModifierIdAsync(modifierId, categoryId, markModifierInactive: true);

        Assert.False(result.AlreadyMigrated);
        Assert.True(result.ModifierMarkedInactive);
        Assert.NotNull(result.ProductId);
        Assert.Equal("Mayo", result.ProductName);
        Assert.Equal(modifierId, result.ModifierId);
        Assert.Equal(groupId, result.GroupId);

        var product = await context.Products.FindAsync(result.ProductId);
        Assert.NotNull(product);
        Assert.True(product.IsSellableAddOn);
        Assert.Equal("Mayo", product.Name);
        Assert.Equal(0.50m, product.Price);
        Assert.Equal(2, product.TaxType);
        Assert.NotNull(product.Description);
        Assert.Equal("Mayo", product.Description);

        var modifier = await context.ProductModifiers.FindAsync(modifierId);
        Assert.NotNull(modifier);
        Assert.False(modifier.IsActive);

        var link = await context.AddOnGroupProducts.FirstOrDefaultAsync(a => a.ProductId == product!.Id && a.ModifierGroupId == groupId);
        Assert.NotNull(link);
        Assert.Equal(1, link.SortOrder);
    }

    /// <summary>Production-safe: single migration path (admin UI) must set Description = mod.Name ?? string.Empty; never null.</summary>
    [Fact]
    public async Task MigrateSingleByModifierId_CreatedProduct_HasDescriptionNeverNull()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Extra Käse",
            Price = 1.50m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateSingleByModifierIdAsync(modifierId, categoryId, markModifierInactive: true);

        Assert.False(result.AlreadyMigrated);
        Assert.NotNull(result.ProductId);
        var product = await context.Products.FindAsync(result.ProductId);
        Assert.NotNull(product);
        Assert.NotNull(product.Description);
        Assert.NotEmpty(product.Description);
        Assert.Equal("Extra Käse", product.Description);
    }

    [Fact]
    public async Task MigrateSingleByModifierId_ModifierNotFound_Throws()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var nonExistentModifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MigrateSingleByModifierIdAsync(nonExistentModifierId, categoryId));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateSingleByModifierId_InactiveModifierWithExistingProduct_ReturnsAlreadyMigrated()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = false
        });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Ketchup",
            Description = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            CategoryId = categoryId,
            Category = "Extras",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            Barcode = "ADDON-xxx",
            IsActive = true,
            IsSellableAddOn = true
        });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = productId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var result = await service.MigrateSingleByModifierIdAsync(modifierId, categoryId);

        Assert.True(result.AlreadyMigrated);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal("Ketchup", result.ProductName);
        Assert.False(result.ModifierMarkedInactive);

        var productCount = await context.Products.CountAsync(p => p.Name == "Ketchup" && p.IsSellableAddOn);
        Assert.Equal(1, productCount);
    }

    [Fact]
    public async Task MigrateSingleByModifierId_InactiveModifierWithoutProduct_Throws()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = 0.30m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MigrateSingleByModifierIdAsync(modifierId, categoryId));

        Assert.Contains("already inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateSingleByModifierId_DuplicateMigration_ReturnsAlreadyMigrated()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Mayo",
            Price = 0.50m,
            TaxType = 2,
            SortOrder = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);

        var first = await service.MigrateSingleByModifierIdAsync(modifierId, categoryId);
        Assert.False(first.AlreadyMigrated);
        Assert.NotNull(first.ProductId);

        var second = await service.MigrateSingleByModifierIdAsync(modifierId, categoryId);
        Assert.True(second.AlreadyMigrated);
        Assert.Equal(first.ProductId, second.ProductId);

        var productCount = await context.Products.CountAsync(p => p.Name == "Mayo" && p.IsSellableAddOn);
        Assert.Equal(1, productCount);
    }

    [Fact]
    public async Task MigrateSingleByModifierId_InvalidCategory_Throws()
    {
        await using var context = CreateContext();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var badCategoryId = Guid.NewGuid();

        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MigrateSingleByModifierIdAsync(modifierId, badCategoryId));

        Assert.Contains("Category", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ========== GetMigrationProgressAsync (Admin GET /api/admin/migration-progress) ==========

    /// <summary>Zero state: no modifiers and no groups with modifiers only.</summary>
    [Fact]
    public async Task GetMigrationProgress_ZeroState_ReturnsZeroCounts()
    {
        await using var context = CreateContext();
        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);

        var progress = await service.GetMigrationProgressAsync();

        Assert.Equal(0, progress.ActiveLegacyModifiersCount);
        Assert.Equal(0, progress.GroupsWithModifiersOnlyCount);
    }

    /// <summary>Active legacy modifier count: only is_active = true modifiers are counted.</summary>
    [Fact]
    public async Task GetMigrationProgress_ActiveLegacyModifiers_ReturnsCorrectCount()
    {
        await using var context = CreateContext();
        var groupId = Guid.NewGuid();
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Extras", SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier { Id = Guid.NewGuid(), ModifierGroupId = groupId, Name = "Ketchup", Price = 0.30m, TaxType = 2, SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier { Id = Guid.NewGuid(), ModifierGroupId = groupId, Name = "Mayo", Price = 0.50m, TaxType = 2, SortOrder = 1, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier { Id = Guid.NewGuid(), ModifierGroupId = groupId, Name = "Inactive", Price = 0.10m, TaxType = 2, SortOrder = 2, IsActive = false });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var progress = await service.GetMigrationProgressAsync();

        Assert.Equal(2, progress.ActiveLegacyModifiersCount);
        Assert.Equal(1, progress.GroupsWithModifiersOnlyCount);
    }

    /// <summary>Groups with modifiers only: group with add-on products is not counted.</summary>
    [Fact]
    public async Task GetMigrationProgress_GroupsWithModifiersOnly_ReturnsCorrectCount()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupWithModifiersOnlyId = Guid.NewGuid();
        var groupWithProductId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupWithModifiersOnlyId, Name = "Only Modifiers", SortOrder = 0, IsActive = true });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupWithProductId, Name = "Has Product", SortOrder = 1, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier { Id = Guid.NewGuid(), ModifierGroupId = groupWithModifiersOnlyId, Name = "Ketchup", Price = 0.30m, TaxType = 2, SortOrder = 0, IsActive = true });
        context.ProductModifiers.Add(new ProductModifier { Id = Guid.NewGuid(), ModifierGroupId = groupWithProductId, Name = "Legacy Mayo", Price = 0.50m, TaxType = 2, SortOrder = 0, IsActive = true });
        context.Products.Add(new Product { Id = productId, Name = "Mayo", Description = "Mayo", Price = 0.50m, TaxType = 2, CategoryId = categoryId, Category = "Extras", StockQuantity = 0, MinStockLevel = 0, Unit = "Stk", Barcode = "ADDON-xxx", IsActive = true, IsSellableAddOn = true });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupWithProductId, ProductId = productId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var service = new ModifierMigrationService(context, NullLogger<ModifierMigrationService>.Instance);
        var progress = await service.GetMigrationProgressAsync();

        Assert.Equal(2, progress.ActiveLegacyModifiersCount);
        Assert.Equal(1, progress.GroupsWithModifiersOnlyCount);
    }
}
