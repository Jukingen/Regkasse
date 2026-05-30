using KasseAPI_Final.Data.CategorySeed;
using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SystemCategoriesTests
{
    [Theory]
    [InlineData("salate", "Salate")]
    [InlineData("Salate", "Salate")]
    [InlineData("Pizza-mittel", "Pizza, mittel")]
    [InlineData("pizza-mittel", "Pizza, mittel")]
    [InlineData("Alkoholfreie-Getrnke", "Alkoholfreie Getränke")]
    public void TryResolve_MatchesKeyDisplayNameAndLegacyLabels(string reference, string expectedDisplayName)
    {
        Assert.True(SystemCategories.TryResolve(reference, out var category));
        Assert.Equal(expectedDisplayName, category.DisplayName);
    }

    [Fact]
    public void CreateDemoCatalogCategories_ContainsSixteenSeedCategories()
    {
        var categories = SystemCategories.CreateDemoCatalogCategories();
        Assert.Equal(16, categories.Count);
        Assert.Contains(categories, c => c.Key == "saucen" && c.VatRate == 20m);
        Assert.Contains(categories, c => c.Key == "alkoholfreie-getranke" && c.FiscalCategory == RksvProductCategory.Beverage);
    }

    [Fact]
    public void ToDemoCategory_MapsSeedMetadata()
    {
        var seed = SystemCategories.DemoCategories.Single(c => c.Key == "pizza-mittel");
        var demo = SystemCategories.ToDemoCategory(seed);

        Assert.Equal("pizza-mittel", demo.Key);
        Assert.Equal("Pizza, mittel", demo.Name);
        Assert.Equal("🍕", demo.Icon);
        Assert.Equal(10m, demo.VatRate);
        Assert.Equal(RksvProductCategory.Food, demo.FiscalCategory);
    }
}
