using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CategoryAppearanceTests
{
    [Theory]
    [InlineData(null, "📦")]
    [InlineData("", "📦")]
    [InlineData("  ", "📦")]
    [InlineData("🥗", "🥗")]
    [InlineData("wine", "🍷")]
    [InlineData("restaurant", "🍽️")]
    [InlineData("cafe", "☕")]
    public void ResolveIcon_UsesEmojiOrLegacyMapOrDefault(string? icon, string expected)
    {
        Assert.Equal(expected, CategoryAppearance.ResolveIcon(icon));
    }

    [Fact]
    public void TryNormalizeColor_AcceptsHexAndExpandsShortForm()
    {
        Assert.True(CategoryAppearance.TryNormalizeColor("#e53935", out var full, out var error));
        Assert.Equal("#E53935", full);
        Assert.Null(error);

        Assert.True(CategoryAppearance.TryNormalizeColor("#7c3", out var shortForm, out _));
        Assert.Equal("#77CC33", shortForm);

        Assert.True(CategoryAppearance.TryNormalizeColor("  ", out var empty, out _));
        Assert.Null(empty);
    }

    [Fact]
    public void TryNormalizeColor_RejectsInvalidValues()
    {
        Assert.False(CategoryAppearance.TryNormalizeColor("red", out _, out var error));
        Assert.Contains("hex", error, StringComparison.OrdinalIgnoreCase);
    }
}
