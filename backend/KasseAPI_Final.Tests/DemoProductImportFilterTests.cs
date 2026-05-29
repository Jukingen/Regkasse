using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoProductImportFilterTests
{
    private static DemoData SampleData()
    {
        var data = new DemoData
        {
            Categories =
            [
                new DemoCategory { Name = "Salate", SortOrder = 1 },
                new DemoCategory { Name = "Pasta", SortOrder = 2 },
                new DemoCategory { Name = "Kebap", SortOrder = 3 },
            ],
            Products =
            [
                new DemoProduct { Name = "chefsalat", Category = "Salate", Price = 9.5m },
                new DemoProduct { Name = "pasta bolognese", Category = "Pasta", Price = 9.5m },
                new DemoProduct { Name = "döner kebap", Category = "Kebap", Price = 5.4m },
            ],
        };
        DemoProductImportFilter.NormalizeDemoProductIds(data);
        return data;
    }

    [Fact]
    public void SelectCategories_WithNoSelection_ReturnsAllCategories()
    {
        var data = SampleData();
        var categories = DemoProductImportFilter.SelectCategories(data, new DemoImportRequest());

        Assert.Equal(3, categories.Count);
    }

    [Fact]
    public void SelectCategories_WithSelectedCategories_FiltersList()
    {
        var data = SampleData();
        var categories = DemoProductImportFilter.SelectCategories(
            data,
            new DemoImportRequest { SelectedCategories = ["Salate", "Pasta"] });

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.Contains(c.Name, new[] { "Salate", "Pasta" }));
    }

    [Fact]
    public void SelectCategories_WithExcludedCategories_SkipsThoseCategories()
    {
        var data = SampleData();
        var categories = DemoProductImportFilter.SelectCategories(
            data,
            new DemoImportRequest { ExcludedCategories = ["Kebap"] });

        Assert.Equal(2, categories.Count);
        Assert.DoesNotContain(categories, c => c.Name == "Kebap");
    }

    [Fact]
    public void SelectCategories_SelectedCategoriesTakePrecedenceOverExcluded()
    {
        var data = SampleData();
        var categories = DemoProductImportFilter.SelectCategories(
            data,
            new DemoImportRequest
            {
                SelectedCategories = ["Salate"],
                ExcludedCategories = ["Salate"],
            });

        Assert.Single(categories);
        Assert.Equal("Salate", categories[0].Name);
    }

    [Fact]
    public void SelectProducts_WithSelectedProductIds_FiltersByDemoId()
    {
        var data = SampleData();
        var categories = data.Categories.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var salatId = data.Products[0].Id;

        var products = DemoProductImportFilter.SelectProducts(
            data,
            categories,
            new DemoImportRequest { SelectedProductIds = [salatId] });

        Assert.Single(products);
        Assert.Equal("chefsalat", products[0].Name);
    }
}
