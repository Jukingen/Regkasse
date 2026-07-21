using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Inventory;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Wave 3C: <see cref="InventoryController"/> must not expose another tenant's inventory by id enumeration (404 instead of 200 + leaked metadata).
/// </summary>
public sealed class InventoryControllerTenantGateTests
{
    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InvTenantGate_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static void EnsureTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "inv-gate-tenant-b" });
    }

    private static InventoryController CreateController(AppDbContext ctx, Guid effectiveTenantId)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogEntityChangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        return new InventoryController(
            ctx,
            NullLogger<InventoryController>.Instance,
            audit.Object,
            TenantTestDoubles.SettingsResolverReturning(effectiveTenantId));
    }

    private static async Task<(Guid catB, Guid prodB, Guid invB)> SeedTenantBInventoryAsync(AppDbContext ctx)
    {
        EnsureTenants(ctx);
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var prodB = Guid.NewGuid();
        ctx.Products.Add(new Product
        {
            Id = prodB,
            TenantId = TenantB,
            Name = "SecretProduct",
            Price = 99m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 50,
            MinStockLevel = 1,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-inv-gate-b",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        var invB = Guid.NewGuid();
        ctx.Inventory.Add(new InventoryItem
        {
            Id = invB,
            ProductId = prodB,
            CurrentStock = 42,
            MinStockLevel = 1,
            UnitCost = 123.45m,
            Notes = "secret-cost-and-notes",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return (catB, prodB, invB);
    }

    private static async Task<(Guid invA, Guid invB)> SeedTwoTenantsWithInventoryAsync(AppDbContext ctx)
    {
        EnsureTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var prodA = Guid.NewGuid();
        var prodB = Guid.NewGuid();
        ctx.Products.Add(new Product
        {
            Id = prodA,
            TenantId = TenantA,
            Name = "PA",
            Price = 1m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-a-inv",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = prodB,
            TenantId = TenantB,
            Name = "PB",
            Price = 1m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 20,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-b-inv",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var invA = Guid.NewGuid();
        var invB = Guid.NewGuid();
        ctx.Inventory.Add(new InventoryItem
        {
            Id = invA,
            ProductId = prodA,
            CurrentStock = 10,
            MinStockLevel = 0,
            UnitCost = 1m,
            IsActive = true
        });
        ctx.Inventory.Add(new InventoryItem
        {
            Id = invB,
            ProductId = prodB,
            CurrentStock = 20,
            MinStockLevel = 0,
            UnitCost = 2m,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return (invA, invB);
    }

    private static string SerializeForAssert(object? value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });

    [Fact]
    public async Task GetInventoryItem_WrongTenant_Returns404_NoLeak()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventoryItem(invB);

        var nr = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(nr.Value);
        var body = SerializeForAssert(nr.Value);
        Assert.DoesNotContain("SecretProduct", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-cost-and-notes", body, StringComparison.Ordinal);
        Assert.DoesNotContain("123.45", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInventoryItem_SameTenant_ReturnsOk_WithJoinedProductName_NotUnknownPlaceholder()
    {
        await using var ctx = CreateContext();
        var (invA, _) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventoryItem(invA);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = SerializeForAssert(ok.Value);
        Assert.Contains("PA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown Product", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretProduct", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetInventory_AsTenantA_ExcludesOtherTenantRows()
    {
        await using var ctx = CreateContext();
        var (invA, invB) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventory();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = SerializeForAssert(ok.Value);
        Assert.Contains(invA.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invB.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PA", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PB", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLowStockItems_AsTenantA_DoesNotIncludeTenantBLowStock()
    {
        await using var ctx = CreateContext();
        await SeedTwoTenantsWithInventoryAsync(ctx);
        var (_, _, invSecret) = await SeedTenantBInventoryAsync(ctx);
        var secretRow = await ctx.Inventory.FirstAsync(i => i.Id == invSecret);
        secretRow.CurrentStock = 0;
        secretRow.MinStockLevel = 10;
        await ctx.SaveChangesAsync();

        var c = CreateController(ctx, TenantA);
        var result = await c.GetLowStockItems();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = SerializeForAssert(ok.Value);
        Assert.DoesNotContain("SecretProduct", json, StringComparison.Ordinal);
        Assert.DoesNotContain(invSecret.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateInventoryItem_OtherTenantsProductId_Returns404_NotCreated()
    {
        await using var ctx = CreateContext();
        var (_, prodB, _) = await SeedTenantBInventoryAsync(ctx);
        var inventoryCountBefore = await ctx.Inventory.CountAsync();

        var c = CreateController(ctx, TenantA);
        var result = await c.CreateInventoryItem(new CreateInventoryItemRequest
        {
            ProductId = prodB,
            InitialStock = 1,
            MinStockLevel = 0,
            UnitCost = 1m
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(inventoryCountBefore, await ctx.Inventory.CountAsync());
    }

    [Fact]
    public async Task GetInventoryTransactions_WrongTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);
        ctx.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryId = invB,
            TransactionType = TransactionType.Restock,
            Quantity = 1,
            UnitCost = 1m,
            TotalCost = 1m,
            TransactionDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventoryTransactions(invB);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetInventoryTransactions_SameTenant_ReturnsOk()
    {
        await using var ctx = CreateContext();
        var (invA, _) = await SeedTwoTenantsWithInventoryAsync(ctx);
        ctx.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryId = invA,
            TransactionType = TransactionType.Restock,
            Quantity = 3,
            UnitCost = 2m,
            TotalCost = 6m,
            Notes = "seed",
            TransactionDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventoryTransactions(invA);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<InventoryTransaction>>(ok.Value);
        var arr = list.ToArray();
        Assert.Single(arr);
        Assert.Equal(3, arr[0].Quantity);
        Assert.Contains("seed", arr[0].Notes ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateInventoryItem_WrongTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.UpdateInventoryItem(invB, new UpdateInventoryItemRequest
        {
            MinStockLevel = 0,
            UnitCost = 0m
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateInventoryItem_SameTenant_ReturnsOk()
    {
        await using var ctx = CreateContext();
        var (invA, _) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.UpdateInventoryItem(invA, new UpdateInventoryItemRequest
        {
            MinStockLevel = 3,
            MaxStockLevel = 100,
            ReorderPoint = 5,
            UnitCost = 9.99m,
            Notes = "updated"
        });

        Assert.IsType<OkObjectResult>(result);
        var row = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invA);
        Assert.Equal(3, row.MinStockLevel);
        Assert.Equal(9.99m, row.UnitCost);
    }

    [Fact]
    public async Task RestockInventory_WrongTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var before = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        var result = await c.RestockInventory(invB, new RestockRequest { Quantity = 5 });

        Assert.IsType<NotFoundObjectResult>(result);
        var after = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        Assert.Equal(before.CurrentStock, after.CurrentStock);
    }

    [Fact]
    public async Task AdjustInventory_WrongTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var before = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        var result = await c.AdjustInventory(invB, new AdjustInventoryRequest { Adjustment = -1, Reason = "probe" });

        Assert.IsType<NotFoundObjectResult>(result);
        var after = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        Assert.Equal(before.CurrentStock, after.CurrentStock);
    }

    [Fact]
    public async Task DeleteInventoryItem_WrongTenant_Returns404_AndLeavesRowActive()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.DeleteInventoryItem(invB);

        Assert.IsType<NotFoundObjectResult>(result);
        var row = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task DeleteInventoryItem_SameTenant_ReturnsOk_SoftDeletes()
    {
        await using var ctx = CreateContext();
        var (invA, _) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.DeleteInventoryItem(invA);

        Assert.IsType<OkObjectResult>(result);
        var row = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invA);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task GetInventoryHistory_FilterByOtherTenantInventoryId_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, invB) = await SeedTenantBInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.GetInventoryHistory(inventoryId: invB);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task TransferInventory_SourceOnOtherTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (invA, invB) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantB);
        var sourceBefore = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invA);
        var targetBefore = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        var result = await c.TransferInventory(invA, new TransferInventoryRequest
        {
            TargetInventoryId = invB,
            Quantity = 1,
            Notes = "cross"
        });

        Assert.IsType<NotFoundObjectResult>(result);
        var sourceAfter = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invA);
        var targetAfter = await ctx.Inventory.AsNoTracking().SingleAsync(i => i.Id == invB);
        Assert.Equal(sourceBefore.CurrentStock, sourceAfter.CurrentStock);
        Assert.Equal(targetBefore.CurrentStock, targetAfter.CurrentStock);
    }

    [Fact]
    public async Task TransferInventory_TargetOnOtherTenant_Returns404()
    {
        await using var ctx = CreateContext();
        var (invA, invB) = await SeedTwoTenantsWithInventoryAsync(ctx);

        var c = CreateController(ctx, TenantA);
        var result = await c.TransferInventory(invA, new TransferInventoryRequest
        {
            TargetInventoryId = invB,
            Quantity = 1,
            Notes = "x"
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
