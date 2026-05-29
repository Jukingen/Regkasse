using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoProductPlaceholderImageGeneratorTests
{
    [Fact]
    public void CreateCategoryPlaceholderPng_ReturnsNonEmptyPng()
    {
        var bytes = DemoProductPlaceholderImageGenerator.CreateCategoryPlaceholderPng("Pizza-mittel", "pizza margherita");

        Assert.NotEmpty(bytes);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public void CreateDefaultFoodPng_ReturnsNonEmptyPng()
    {
        var bytes = DemoProductPlaceholderImageGenerator.CreateDefaultFoodPng();

        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData("none", DemoImportImageMode.None)]
    [InlineData("categoryPlaceholder", DemoImportImageMode.CategoryPlaceholder)]
    [InlineData("defaultAsset", DemoImportImageMode.DefaultAsset)]
    public void ParseImageMode_AcceptsAliases(string value, DemoImportImageMode expected)
    {
        Assert.Equal(expected, DemoProductImportImageModeParser.Parse(value));
    }
}
