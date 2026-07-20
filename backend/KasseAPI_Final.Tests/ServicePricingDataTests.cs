using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DigitalServices;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ServicePricingDataTests
{
    [Fact]
    public void GetPricing_returns_four_catalog_entries()
    {
        var pricing = ServicePricingData.GetPricing();
        Assert.Equal(4, pricing.Count);
        Assert.Contains(pricing, p => p.ServiceId == "website-starter");
        Assert.Contains(pricing, p => p.ServiceId == "website-professional");
        Assert.Contains(pricing, p => p.ServiceId == "app-pwa");
        Assert.Contains(pricing, p => p.ServiceId == "app-native");
    }

    [Fact]
    public void GetByType_website_returns_two()
    {
        var website = ServicePricingData.GetByType(ServicePricingTypes.Website);
        Assert.Equal(2, website.Count);
        Assert.All(website, p => Assert.Equal(ServicePricingTypes.Website, p.Type));
    }

    [Fact]
    public void GetByServiceId_returns_expected_prices()
    {
        var starter = ServicePricingData.GetByServiceId("website-starter");
        Assert.NotNull(starter);
        Assert.Equal(99m, starter.PriceMonthly);
        Assert.Equal(990m, starter.PriceYearly);
        Assert.Equal("EUR", starter.Currency);
        Assert.Contains("Statik web sitesi", starter.Features);
    }

    [Fact]
    public void DigitalServicePricingService_filters_by_type()
    {
        IDigitalServicePricingService sut = new DigitalServicePricingService();
        var apps = sut.GetPricing("app");
        Assert.Equal(2, apps.Count);
        Assert.All(apps, p => Assert.Equal(ServicePricingTypes.App, p.Type));
    }
}
