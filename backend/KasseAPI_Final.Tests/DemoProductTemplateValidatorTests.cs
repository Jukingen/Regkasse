using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoProductTemplateValidatorTests
{
    [Fact]
    public void Validate_ValidProductAndCategoryRows_IsValid()
    {
        var rows = new List<DemoTemplateParsedRow>
        {
            new()
            {
                RowNumber = 2,
                RowType = "category",
                Name = "Salate",
                Description = "Fresh salads",
                SortOrderRaw = "1",
                VatRateRaw = "10",
            },
            new()
            {
                RowNumber = 3,
                RowType = "product",
                Name = "Chef Salat",
                Category = "Salate",
                PriceRaw = "9,50",
                TaxRateRaw = "10",
            },
        };

        var result = DemoProductTemplateValidator.Validate(rows, parseError: null, maxPreviewRows: 10);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.CategoryCount);
        Assert.Equal(1, result.ProductCount);
    }

    [Fact]
    public void Validate_MissingProductPrice_HasError()
    {
        var rows = new List<DemoTemplateParsedRow>
        {
            new()
            {
                RowNumber = 2,
                RowType = "product",
                Name = "No Price",
                Category = "Salate",
            },
        };

        var result = DemoProductTemplateValidator.Validate(rows, parseError: null, maxPreviewRows: 10);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Severity == "error");
    }

    [Fact]
    public void BuildDemoData_AutoCreatesCategoryFromProduct()
    {
        var rows = new List<DemoTemplateParsedRow>
        {
            new()
            {
                RowNumber = 2,
                RowType = "product",
                Name = "Pizza Margherita",
                Category = "Pizza-mittel",
                PriceRaw = "9.80",
                TaxRateRaw = "10",
            },
        };

        var (data, error) = DemoProductTemplateValidator.BuildDemoData(rows);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Single(data!.Categories);
        Assert.Equal("Pizza, mittel", data.Categories[0].Name);
        Assert.Equal("pizza-mittel", data.Categories[0].Key);
        Assert.Single(data.Products);
        Assert.NotEqual(Guid.Empty, data.Products[0].Id);
    }
}
