using System.Reflection;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Regression tests for category products endpoint DTO projection.
/// </summary>
public class CategoriesControllerProductsDtoTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CategoryProducts_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetCategoryProducts_ReturnsFlatDtoProjection_WithoutNavigationProperties()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = categoryId,
            Name = "Getranke",
            Description = "Desc",
            VatRate = 20m,
            IsActive = true
        });

        context.Products.Add(new Product
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = productId,
            Name = "Cola",
            Description = "Soft drink",
            Price = 3.50m,
            TaxType = 1,
            TaxRate = 20m,
            ImageUrl = "https://cdn.example/cola.png",
            StockQuantity = 10,
            MinStockLevel = 2,
            Unit = "Stk",
            Cost = 1.20m,
            Barcode = "ABC-123",
            Category = "Getranke",
            CategoryId = categoryId,
            IsActive = true
        });

        await context.SaveChangesAsync();

        var controller = new CategoriesController(context, NullLogger<CategoriesController>.Instance, TenantTestDoubles.PrimaryTenantResolver);
        var result = await controller.GetCategoryProducts(categoryId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IEnumerable<AdminCategoryProductDto>>(ok.Value);
        var dto = Assert.Single(payload);

        Assert.Equal(productId, dto.Id);
        Assert.Equal("Cola", dto.Name);
        Assert.Equal(3.50m, dto.Price);
        Assert.Equal(categoryId, dto.CategoryId);
        Assert.Equal("Getranke", dto.CategoryName);

        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("CategoryNavigation", json);
        Assert.DoesNotContain("Products", json);
    }

    [Fact]
    public void AdminCategoryProductDto_DoesNotExposeNavigationMembers()
    {
        var props = typeof(AdminCategoryProductDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("CategoryNavigation", props);
        Assert.DoesNotContain("ModifierGroupAssignments", props);
        Assert.Contains("CategoryName", props);
        Assert.Contains("CategoryId", props);
    }
}
