using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data;

/// <summary>Legacy dev/bootstrap category seed — upserts by tenant + key/name to avoid unique constraint violations.</summary>
public static class CategorySeedData
{
    private sealed record LegacyDevCategorySeed(
        string Name,
        string Description,
        string Color,
        string Icon,
        int SortOrder,
        decimal VatRate,
        RksvProductCategory FiscalCategory);

    private static readonly LegacyDevCategorySeed[] LegacyDevCategories =
    [
        new("Getränke", "Alkoholfreie und alkoholische Getränke", "#3498db", "wine", 1, 20m, RksvProductCategory.Beverage),
        new("Speisen", "Hauptgerichte und Vorspeisen", "#e74c3c", "restaurant", 2, 20m, RksvProductCategory.Food),
        new("Desserts", "Süße Nachspeisen und Kuchen", "#f39c12", "ice-cream", 3, 10m, RksvProductCategory.Food),
        new("Snacks", "Kleine Zwischenmahlzeiten", "#27ae60", "fast-food", 4, 10m, RksvProductCategory.Food),
        new("Kaffee & Tee", "Heiße Getränke", "#8e44ad", "cafe", 5, 13m, RksvProductCategory.Beverage),
    ];

    public static async Task<int> SeedLegacyDevCategoriesAsync(
        AppDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var createdCount = 0;
        var now = DateTime.UtcNow;

        foreach (var seed in LegacyDevCategories)
        {
            var categoryKey = CategoryKey.FromDisplayName(seed.Name);
            var normalizedName = seed.Name.ToLowerInvariant();

            var category = await context.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    c => c.TenantId == tenantId
                        && (c.Key == categoryKey || c.Name.ToLower() == normalizedName),
                    cancellationToken)
                .ConfigureAwait(false);

            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = categoryKey,
                    Name = seed.Name,
                    Description = seed.Description,
                    Color = seed.Color,
                    Icon = seed.Icon,
                    SortOrder = seed.SortOrder,
                    VatRate = seed.VatRate,
                    FiscalCategory = seed.FiscalCategory,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = "legacy-dev-seed",
                };
                context.Categories.Add(category);
                createdCount++;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (string.IsNullOrWhiteSpace(category.Key))
                category.Key = categoryKey;

            var changed = false;
            if (category.Description != seed.Description)
            {
                category.Description = seed.Description;
                changed = true;
            }

            if (category.SortOrder != seed.SortOrder)
            {
                category.SortOrder = seed.SortOrder;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(category.Color) && !string.IsNullOrWhiteSpace(seed.Color))
            {
                category.Color = seed.Color;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(category.Icon) && !string.IsNullOrWhiteSpace(seed.Icon))
            {
                category.Icon = seed.Icon;
                changed = true;
            }

            if (changed)
            {
                category.UpdatedAt = now;
                category.UpdatedBy = "legacy-dev-seed";
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return createdCount;
    }
}
