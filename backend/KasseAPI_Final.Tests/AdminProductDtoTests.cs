using System.Reflection;
using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Regression: Admin product API returns flat DTO only; no navigation properties to avoid JSON cycles.
/// </summary>
public class AdminProductDtoTests
{
    [Fact]
    public void FromProduct_ReturnsFlatDto_WithMappedScalars()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Price = 9.99m,
            Category = "Getränke",
            CategoryId = Guid.NewGuid(),
            TaxType = 1,
            TaxRate = 20m,
            Barcode = "123",
            IsActive = true,
            Unit = "pcs",
            StockQuantity = 10,
            MinStockLevel = 1,
            Cost = 5m
        };

        var dto = AdminProductDto.FromProduct(product);

        Assert.NotNull(dto);
        Assert.Equal(product.Id, dto.Id);
        Assert.Equal(product.Name, dto.Name);
        Assert.Equal(product.Price, dto.Price);
        Assert.Equal(product.Category, dto.Category);
        Assert.Equal(product.CategoryId, dto.CategoryId);
        Assert.Equal(product.TaxType, dto.TaxType);
        Assert.Equal(product.TaxRate, dto.TaxRate);
    }

    [Fact]
    public void AdminProductDto_HasNoNavigationProperties_ToPreventJsonCycle()
    {
        var dtoType = typeof(AdminProductDto);
        var props = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

        Assert.DoesNotContain("CategoryNavigation", props);
        Assert.DoesNotContain("ModifierGroupAssignments", props);
        Assert.Contains("Category", props);
        Assert.Contains("CategoryId", props);
    }

    [Fact]
    public void FromProduct_SerializesToJson_WithoutCycle()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Serialization Test",
            Price = 1m,
            Category = "Test",
            CategoryId = Guid.NewGuid(),
            TaxType = 1,
            TaxRate = 20m,
            Barcode = "",
            IsActive = true,
            Unit = "pcs",
            StockQuantity = 0,
            MinStockLevel = 0
        };

        var dto = AdminProductDto.FromProduct(product);
        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("CategoryNavigation", json);
        Assert.DoesNotContain("ModifierGroupAssignments", json);
        Assert.Contains("\"Name\":\"Serialization Test\"", json);
    }
}
