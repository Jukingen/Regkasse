using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Catalog payload structure for POS add-on rendering.
/// Ensures: product.modifierGroups[].products is primary; legacy modifiers secondary.
/// </summary>
public class CatalogStructureTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"CatalogStructure_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static void SetAuth(ProductController controller, string userId = "u1")
    {
        var identity = new System.Security.Claims.ClaimsIdentity();
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
        };
    }

    /// <summary>Extract CatalogResponseDto from SuccessResponse wrapper. Anonymous type property name may vary.</summary>
    private static object? GetCatalogDataFromResponse(object? response)
    {
        if (response == null) return null;
        foreach (var prop in response.GetType().GetProperties())
        {
            var val = prop.GetValue(response);
            if (val?.GetType().GetProperty("Products") != null) return val;
        }
        return null;
    }

    /// <summary>Catalog returns products with modifierGroups; each group has products (primary) and modifiers (legacy).</summary>
    [Fact]
    public async Task GetCatalog_WithProductAndAddOnGroup_ReturnsModifierGroupsWithProducts()
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
            ProductId = addOnProductId,
            SortOrder = 0
        });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = mainProductId,
            ModifierGroupId = groupId,
            SortOrder = 0
        });
        await context.SaveChangesAsync();

        var productRepo = new GenericRepository<Product>(context, NullLogger<GenericRepository<Product>>.Instance);
        var controller = new ProductController(context, productRepo, NullLogger<ProductController>.Instance);
        SetAuth(controller);

        var result = await controller.GetCatalog();
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);

        var response = ok.Value;
        Assert.NotNull(response);
        var data = GetCatalogDataFromResponse(response) as CatalogResponseDto;
        Assert.NotNull(data);

        Assert.NotNull(data.Products);
        Assert.NotEmpty(data.Products);

        // Catalog orders by Category then Name; add-on (Extra Käse) can be first — find product that has modifier groups
        System.Collections.IList? modifierGroups = null;
        var modifierGroupsProp = typeof(CatalogProductDto).GetProperty("ModifierGroups");
        Assert.NotNull(modifierGroupsProp);
        foreach (var p in data.Products)
        {
            var mg = modifierGroupsProp.GetValue(p) as System.Collections.IList;
            if (mg != null && mg.Count > 0) { modifierGroups = mg; break; }
        }
        Assert.NotNull(modifierGroups);
        Assert.Single(modifierGroups);

        var group = modifierGroups![0];
        var productsInGroupProp = group!.GetType().GetProperty("Products");
        Assert.NotNull(productsInGroupProp);
        var productsInGroup = productsInGroupProp.GetValue(group) as System.Collections.IList;
        Assert.NotNull(productsInGroup);
        Assert.Single(productsInGroup!);

        var addOn = productsInGroup[0];
        var productNameProp = addOn!.GetType().GetProperty("ProductName");
        var priceProp = addOn.GetType().GetProperty("Price");
        Assert.NotNull(productNameProp);
        Assert.NotNull(priceProp);
        Assert.Equal("Extra Käse", productNameProp.GetValue(addOn));
        Assert.Equal(1.50m, Convert.ToDecimal(priceProp.GetValue(addOn)));
    }

    /// <summary>Group with only products (no legacy modifiers) still returns correct structure. POS does not require modifiers.</summary>
    [Fact]
    public async Task GetCatalog_GroupWithOnlyProductsNoModifiers_ReturnsProductsArray()
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
            Name = "Pizza",
            Price = 8.50m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 5,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.Products.Add(new Product
        {
            Id = addOnProductId,
            Name = "Oliven",
            Price = 0.80m,
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
            Name = "Toppings",
            SortOrder = 0,
            IsActive = true
        });
        context.AddOnGroupProducts.Add(new AddOnGroupProduct
        {
            ModifierGroupId = groupId,
            ProductId = addOnProductId,
            SortOrder = 0
        });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = mainProductId,
            ModifierGroupId = groupId,
            SortOrder = 0
        });
        await context.SaveChangesAsync();

        var productRepo = new GenericRepository<Product>(context, NullLogger<GenericRepository<Product>>.Instance);
        var controller = new ProductController(context, productRepo, NullLogger<ProductController>.Instance);
        SetAuth(controller);

        var result = await controller.GetCatalog();
        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(ok);

        var response = ok.Value;
        var data = GetCatalogDataFromResponse(response) as CatalogResponseDto;
        Assert.NotNull(data);
        Assert.NotNull(data.Products);
        Assert.NotEmpty(data.Products);
        var modifierGroupsProp = typeof(CatalogProductDto).GetProperty("ModifierGroups");
        Assert.NotNull(modifierGroupsProp);
        System.Collections.IList? modifierGroups = null;
        foreach (var p in data.Products)
        {
            var mg = modifierGroupsProp.GetValue(p) as System.Collections.IList;
            if (mg != null && mg.Count > 0) { modifierGroups = mg; break; }
        }
        Assert.NotNull(modifierGroups);
        var group = modifierGroups![0];
        var productsInGroupProp = group!.GetType().GetProperty("Products");
        var productsInGroup = productsInGroupProp?.GetValue(group) as System.Collections.IList;
        Assert.NotNull(productsInGroup);
        Assert.Single(productsInGroup!);
        Assert.Equal("Oliven", productsInGroup[0]!.GetType().GetProperty("ProductName")!.GetValue(productsInGroup[0]));
    }

    /// <summary>CatalogResponseDto serializes to camelCase; modifierGroups.products is present for POS consumption.</summary>
    [Fact]
    public void CatalogProductDto_SerializesWithModifierGroupsProducts_CamelCase()
    {
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var addOnId = Guid.NewGuid();

        var dto = new CatalogProductDto
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = Guid.NewGuid(),
            TaxType = 2,
            IsActive = true,
            ModifierGroups =
            {
                new ModifierGroupDto
                {
                    Id = groupId,
                    Name = "Extras",
                    Products =
                    {
                        new AddOnGroupProductItemDto
                        {
                            ProductId = addOnId,
                            ProductName = "Extra Käse",
                            Price = 1.50m,
                            TaxType = 2,
                            SortOrder = 0
                        }
                    }
                }
            }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(dto, options);
        Assert.Contains("modifierGroups", json);
        Assert.Contains("products", json);
        Assert.Contains("productName", json);
        Assert.Contains("Extra", json); // "Käse" may be Unicode-escaped as \u00e4 in JSON

        var roundTrip = JsonSerializer.Deserialize<CatalogProductDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(roundTrip);
        Assert.Single(roundTrip.ModifierGroups);
        Assert.Single(roundTrip.ModifierGroups[0].Products);
        Assert.Equal("Extra Käse", roundTrip.ModifierGroups[0].Products[0].ProductName);
    }
}
