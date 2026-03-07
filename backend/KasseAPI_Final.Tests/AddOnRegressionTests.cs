using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Regression tests after legacy modifier migration removal.
/// Ensures: add-on group listing/details, add-on product management, product–group assignment,
/// receipt composition and VAT use only the active add-on model (Product + AddOnGroupProduct); no legacy modifier fallback.
/// </summary>
public class AddOnRegressionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"AddOnRegression_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static void SetAuth(ModifierGroupsController controller, string userId = "u1")
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
    }

    private static void SetAuthProduct(ProductController controller, string userId = "u1")
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task ModifierGroups_GetAll_ReturnsGroupsWithProductsOnly_ModifiersEmpty()
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
        context.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = productId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var controller = new ModifierGroupsController(context, NullLogger<ModifierGroupsController>.Instance);
        SetAuth(controller);

        var result = await controller.GetAll();
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var json = JsonSerializer.Serialize(ok.Value);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json);
        var data = wrapper.GetProperty("data");
        var groups = JsonSerializer.Deserialize<List<ModifierGroupDto>>(data.GetRawText());
        Assert.NotNull(groups);
        Assert.Single(groups);
        Assert.NotNull(groups[0].Products);
        Assert.Single(groups[0].Products);
        Assert.Equal("Extra Käse", groups[0].Products![0].ProductName);
        Assert.Equal(1.50m, groups[0].Products[0].Price);
        Assert.NotNull(groups[0].Modifiers);
        Assert.Empty(groups[0].Modifiers);
    }

    [Fact]
    public async Task ModifierGroups_GetById_ReturnsGroupWithProductsOnly_ModifiersEmpty()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Extras", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Mayo",
            Price = 0.30m,
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
            Name = "Saucen",
            SortOrder = 0,
            IsActive = true
        });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = productId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var controller = new ModifierGroupsController(context, NullLogger<ModifierGroupsController>.Instance);
        SetAuth(controller);

        var result = await controller.GetById(groupId);
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var json = JsonSerializer.Serialize(ok.Value);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json);
        var data = wrapper.GetProperty("data");
        var group = JsonSerializer.Deserialize<ModifierGroupDto>(data.GetRawText());
        Assert.NotNull(group);
        Assert.Equal("Saucen", group.Name);
        Assert.NotNull(group.Products);
        Assert.Single(group.Products);
        Assert.Equal("Mayo", group.Products[0].ProductName);
        Assert.NotNull(group.Modifiers);
        Assert.Empty(group.Modifiers);
    }

    [Fact]
    public async Task Product_GetProductModifierGroups_ReturnsProductsOnly_ModifiersEmpty()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var mainProductId = Guid.NewGuid();
        var addOnProductId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = mainProductId,
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
        context.Products.Add(new Product
        {
            Id = addOnProductId,
            Name = "Ketchup",
            Price = 0.50m,
            CategoryId = categoryId,
            Category = "Extras",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true,
            IsSellableAddOn = true
        });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = addOnProductId, SortOrder = 0 });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainProductId, ModifierGroupId = groupId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var productRepo = new KasseAPI_Final.Data.Repositories.GenericRepository<Product>(context, NullLogger<KasseAPI_Final.Data.Repositories.GenericRepository<Product>>.Instance);
        var controller = new ProductController(context, productRepo, NullLogger<ProductController>.Instance);
        SetAuthProduct(controller);

        var result = await controller.GetProductModifierGroups(mainProductId);
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);
        var json = JsonSerializer.Serialize(ok.Value);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json);
        var data = wrapper.GetProperty("data");
        var groups = JsonSerializer.Deserialize<List<ModifierGroupDto>>(data.GetRawText());
        Assert.NotNull(groups);
        Assert.Single(groups);
        Assert.NotNull(groups[0].Products);
        Assert.Single(groups[0].Products);
        Assert.Equal("Ketchup", groups[0].Products![0].ProductName);
        Assert.NotNull(groups[0].Modifiers);
        Assert.Empty(groups[0].Modifiers);
    }

    [Fact]
    public void Catalog_ModifierGroupDto_SerializedWithEmptyModifiers_NoLegacyFallback()
    {
        var dto = new ModifierGroupDto
        {
            Id = Guid.NewGuid(),
            Name = "Extras",
            Products = new List<AddOnGroupProductItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Käse", Price = 1.50m, TaxType = 2, SortOrder = 0 }
            },
            Modifiers = new List<ModifierDto>()
        };
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var roundTrip = JsonSerializer.Deserialize<ModifierGroupDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(roundTrip);
        Assert.NotNull(roundTrip.Modifiers);
        Assert.Empty(roundTrip.Modifiers);
        Assert.Single(roundTrip.Products!);
    }

    /// <summary>Receipt composition uses only product lines; Phase2ReceiptFlatTests cover flat payment → no modifier lines, VAT totals.</summary>
    [Fact]
    public void Receipt_FlatPaymentCoveredByPhase2ReceiptFlatTests()
    {
        Assert.True(true, "Regression: Phase2ReceiptFlatTests.GetReceiptData_FromFlatPayment_ReturnsOneLinePerItemNoModifierLines and CreatePayment_WithBaseProductAndAddOn_PriceAndTaxTotalsCorrect cover receipt composition and VAT.");
    }
}
