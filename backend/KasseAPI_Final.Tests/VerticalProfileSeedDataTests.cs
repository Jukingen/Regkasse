using System.Text.Json;
using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class VerticalProfileSeedDataTests
{
    [Fact]
    public void RetailSeed_IsStandardLayout_WithSkuAndFutureFlagsOff()
    {
        var retail = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.Retail);

        Assert.Equal("verticalProfiles.retail.name", retail.Name);
        Assert.Equal(VerticalProfileLayouts.Standard, retail.PosLayout);

        using var features = JsonDocument.Parse(retail.PosFeatures);
        Assert.True(features.RootElement.GetProperty("variants").GetBoolean());
        Assert.False(features.RootElement.GetProperty("lotTracking").GetBoolean());
        Assert.False(features.RootElement.GetProperty("multiBranch").GetBoolean());
        Assert.True(features.RootElement.GetProperty("barcodeScan").GetBoolean());

        using var required = JsonDocument.Parse(retail.RequiredFields);
        var productFields = required.RootElement.GetProperty("product").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains("sku", productFields);
        Assert.Contains("name", productFields);
        Assert.Contains("price", productFields);
    }

    [Fact]
    public void NonRetailSeeds_SetVariantsFalse()
    {
        foreach (var profile in VerticalProfileSeedData.All.Where(item => item.Id != VerticalProfileIds.Retail))
        {
            using var features = JsonDocument.Parse(profile.PosFeatures);
            Assert.True(features.RootElement.TryGetProperty("variants", out var variants), profile.Id);
            Assert.False(variants.GetBoolean(), profile.Id);
            Assert.True(features.RootElement.TryGetProperty("barcodeScan", out var barcodeScan), profile.Id);
            Assert.False(barcodeScan.GetBoolean(), profile.Id);
        }
    }
}
