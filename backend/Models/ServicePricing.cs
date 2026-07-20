namespace KasseAPI_Final.Models;

/// <summary>
/// Digital service list price (website / app add-on). Static catalog — not a fiscal or license_sales row.
/// </summary>
public sealed class ServicePricing
{
    public required string ServiceId { get; init; }
    public required string Name { get; init; }
    /// <summary><see cref="ServicePricingTypes"/> — website or app.</summary>
    public required string Type { get; init; }
    /// <summary>starter / professional / premium / pwa / native.</summary>
    public required string Tier { get; init; }
    public decimal PriceMonthly { get; init; }
    public decimal PriceYearly { get; init; }
    public required string[] Features { get; init; }
    /// <summary>ISO 4217; digital add-ons are priced in EUR.</summary>
    public string Currency { get; init; } = "EUR";
}

/// <summary>Allowed <see cref="ServicePricing.Type"/> values.</summary>
public static class ServicePricingTypes
{
    public const string Website = "website";
    public const string App = "app";
}

/// <summary>Static digital-service price list for FA billing / customer portal.</summary>
public static class ServicePricingData
{
    public static IReadOnlyList<ServicePricing> GetPricing() => Catalog;

    public static ServicePricing? GetByServiceId(string? serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return null;
        return Catalog.FirstOrDefault(p =>
            string.Equals(p.ServiceId, serviceId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<ServicePricing> GetByType(string type) =>
        Catalog
            .Where(p => string.Equals(p.Type, type, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static readonly IReadOnlyList<ServicePricing> Catalog =
    [
        new ServicePricing
        {
            ServiceId = "website-starter",
            Name = "Web Sitesi Starter",
            Type = ServicePricingTypes.Website,
            Tier = "starter",
            PriceMonthly = 99,
            PriceYearly = 990,
            Features =
            [
                "Statik web sitesi",
                "Menü gösterimi",
                "İletişim"
            ]
        },
        new ServicePricing
        {
            ServiceId = "website-professional",
            Name = "Web Sitesi Professional",
            Type = ServicePricingTypes.Website,
            Tier = "professional",
            PriceMonthly = 299,
            PriceYearly = 2990,
            Features =
            [
                "Dinamik web sitesi",
                "Online sipariş",
                "Rezervasyon"
            ]
        },
        new ServicePricing
        {
            ServiceId = "app-pwa",
            Name = "PWA App",
            Type = ServicePricingTypes.App,
            Tier = "pwa",
            PriceMonthly = 199,
            PriceYearly = 1990,
            Features =
            [
                "Web tabanlı",
                "Push bildirim",
                "Offline destek"
            ]
        },
        new ServicePricing
        {
            ServiceId = "app-native",
            Name = "Native App",
            Type = ServicePricingTypes.App,
            Tier = "native",
            PriceMonthly = 499,
            PriceYearly = 4990,
            Features =
            [
                "iOS/Android",
                "Native performans",
                "Store yayını"
            ]
        }
    ];
}
