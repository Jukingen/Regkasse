using KasseAPI_Final.Models;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Data.CategorySeed;

public sealed class SystemCategory
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public decimal DefaultTaxRate { get; init; } = 10m;
    public RksvProductCategory FiscalCategory { get; init; } = RksvProductCategory.Food;
    public int SortOrder { get; init; }
}

/// <summary>Predefined demo/system categories — source of truth for fiscal metadata and display defaults.</summary>
public static class SystemCategories
{
    public static readonly IReadOnlyList<SystemCategory> DemoCategories =
    [
        new() { Key = "salate", DisplayName = "Salate", Description = "Alle Salate werden mit einem Dressing nach Wahl zubereitet.", Icon = "🥗", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 10 },
        new() { Key = "stangerl", DisplayName = "Stangerl", Description = "Alle Stangerl werden mit Tomaten, Käse und Oregano zubereitet.", Icon = "🥪", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 20 },
        new() { Key = "baguettes", DisplayName = "Baguettes", Description = "Alle Baguettes werden mit Tomaten, Käse und Oregano zubereitet.", Icon = "🥖", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 30 },
        new() { Key = "calzone", DisplayName = "Calzone", Description = "Alle Calzonen werden mit Tomaten, Käse und Oregano zubereitet.", Icon = "🥟", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 40 },
        new() { Key = "pizza-mittel", DisplayName = "Pizza, mittel", Description = "Ø 36cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.", Icon = "🍕", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 50 },
        new() { Key = "pizza-partner", DisplayName = "Pizza, Partner", Description = "Ø 40cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.", Icon = "🍕", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 60 },
        new() { Key = "familien-pizza", DisplayName = "Familien-Pizza", Description = "Ø 50cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.", Icon = "🍕", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 70 },
        new() { Key = "mexikanische-pizza-mittel", DisplayName = "Mexikanische Pizza, mittel", Description = "Ø 36cm. Alle Pizzen werden mit Jalapenos und Tacosauce zubereitet.", Icon = "🌶️", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 80 },
        new() { Key = "mexikanische-pizza-partner", DisplayName = "Mexikanische Pizza, Partner", Description = "Ø 40cm. Alle Pizzen werden mit Jalapenos und Tacosauce zubereitet.", Icon = "🌶️", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 90 },
        new() { Key = "pasta", DisplayName = "Pasta", Description = "Alle Gerichte werden mit einer Nudelsorte nach Wahl zubereitet.", Icon = "🍝", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 100 },
        new() { Key = "imbiss", DisplayName = "Imbiss", Description = "Alle Gerichte werden mit einem Dip nach Wahl serviert.", Icon = "🍟", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 110 },
        new() { Key = "burger", DisplayName = "Burger", Description = "Alle Burger werden mit Pommes frites serviert.", Icon = "🍔", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 120 },
        new() { Key = "kebap", DisplayName = "Kebap", Description = "Alle Gerichte werden mit Salat, Tomaten, Zwiebeln, Rotkraut und einer Sauce nach Wahl zubereitet.", Icon = "🥙", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 130 },
        new() { Key = "desserts", DisplayName = "Desserts", Description = "", Icon = "🍰", DefaultTaxRate = 10, FiscalCategory = RksvProductCategory.Food, SortOrder = 140 },
        new() { Key = "saucen", DisplayName = "Saucen", Description = "", Icon = "🥫", DefaultTaxRate = 20, FiscalCategory = RksvProductCategory.Food, SortOrder = 150 },
        new() { Key = "alkoholfreie-getranke", DisplayName = "Alkoholfreie Getränke", Description = "", Icon = "🥤", DefaultTaxRate = 20, FiscalCategory = RksvProductCategory.Beverage, SortOrder = 160 },
    ];

    private static readonly Dictionary<string, SystemCategory> ByKey =
        DemoCategories.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, SystemCategory> ByReference = BuildReferenceIndex();

    public static bool TryResolve(string? reference, out SystemCategory category)
    {
        category = null!;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var trimmed = reference.Trim();
        if (ByReference.TryGetValue(trimmed, out var resolved))
        {
            category = resolved;
            return true;
        }

        var slug = CategoryKey.FromDisplayName(trimmed);
        if (ByKey.TryGetValue(slug, out resolved))
        {
            category = resolved;
            return true;
        }

        return false;
    }

    public static DemoCategory ToDemoCategory(SystemCategory source) =>
        new()
        {
            Key = source.Key,
            Name = source.DisplayName,
            Description = source.Description,
            Icon = source.Icon,
            SortOrder = source.SortOrder,
            VatRate = source.DefaultTaxRate,
            FiscalCategory = source.FiscalCategory,
        };

    public static IReadOnlyList<DemoCategory> CreateDemoCatalogCategories() =>
        DemoCategories.Select(ToDemoCategory).ToList();

    public static void NormalizeProductReferences(DemoData data)
    {
        foreach (var product in data.Products)
        {
            if (TryResolve(product.Category, out var category))
                product.Category = category.DisplayName;
        }
    }

    public static DemoCategory ResolveOrCreateAdHocCategory(string categoryReference, decimal fallbackVatRate)
    {
        if (TryResolve(categoryReference, out var systemCategory))
            return ToDemoCategory(systemCategory);

        return new DemoCategory
        {
            Key = CategoryKey.FromDisplayName(categoryReference),
            Name = categoryReference.Trim(),
            SortOrder = DemoCategories.Count + 1,
            VatRate = fallbackVatRate,
            FiscalCategory = CategoryKey.InferFiscalCategory(categoryReference),
        };
    }

    private static Dictionary<string, SystemCategory> BuildReferenceIndex()
    {
        var index = new Dictionary<string, SystemCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in DemoCategories)
        {
            index[category.Key] = category;
            index[category.DisplayName] = category;
        }

        // Legacy demo-products.json category labels
        index["Pizza-mittel"] = ByKey["pizza-mittel"];
        index["Pizza-Partner"] = ByKey["pizza-partner"];
        index["Mexikanische-Pizza-mittel"] = ByKey["mexikanische-pizza-mittel"];
        index["Mexikanische-Pizza-Partner"] = ByKey["mexikanische-pizza-partner"];
        index["Alkoholfreie-Getrnke"] = ByKey["alkoholfreie-getranke"];

        return index;
    }
}
