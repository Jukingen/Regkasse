using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data;

/// <summary>Default Austrian MwSt tax groups seeded per tenant.</summary>
public static class TaxGroupSeedData
{
    public static TaxGroup[] GetTaxGroups(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        return
        [
            new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Normalsatz",
                Description = "20% MwSt. - Standard",
                Rate = 20m,
                IsActive = true,
                IsDefault = true,
                IsSystem = true,
                Color = "#1890ff",
                Icon = "💰",
                AustrianCode = "A",
                GroupType = TaxGroupType.Standard,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "tax-group-seed",
            },
            new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ermäßigt",
                Description = "10% MwSt. - Lebensmittel, Bücher",
                Rate = 10m,
                IsActive = true,
                IsSystem = true,
                Color = "#52c41a",
                Icon = "🛒",
                AustrianCode = "B",
                GroupType = TaxGroupType.Reduced,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "tax-group-seed",
            },
            new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ermäßigt (Neu)",
                Description = "4,9% MwSt. - E-Books, bestimmte Lebensmittel",
                Rate = 4.9m,
                IsActive = true,
                IsSystem = true,
                Color = "#faad14",
                Icon = "📚",
                AustrianCode = "C",
                GroupType = TaxGroupType.ReducedNew,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "tax-group-seed",
            },
            new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Mittelsteuersatz",
                Description = "13% MwSt. - Tourismus, Dienstleistungen",
                Rate = 13m,
                IsActive = true,
                IsSystem = true,
                Color = "#722ed1",
                Icon = "🏨",
                AustrianCode = "D",
                GroupType = TaxGroupType.Middle,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "tax-group-seed",
            },
            new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Nullsteuersatz",
                Description = "0% MwSt. - Export",
                Rate = 0m,
                IsActive = true,
                IsSystem = true,
                Color = "#8c8c8c",
                Icon = "🌍",
                AustrianCode = "E",
                GroupType = TaxGroupType.Zero,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "tax-group-seed",
            },
        ];
    }

    /// <summary>
    /// Upserts system tax groups by tenant + AustrianCode (falls back to Rate when code is empty).
    /// Returns the number of newly created rows.
    /// </summary>
    public static async Task<int> SeedSystemTaxGroupsAsync(
        AppDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var createdCount = 0;
        var now = DateTime.UtcNow;
        var seeds = GetTaxGroups(tenantId);

        foreach (var seed in seeds)
        {
            var existing = await context.TaxGroups
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    g => g.TenantId == tenantId
                        && (
                            (seed.AustrianCode != null && g.AustrianCode == seed.AustrianCode)
                            || (seed.AustrianCode == null && g.Rate == seed.Rate && g.IsSystem)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing == null)
            {
                seed.CreatedAt = now;
                seed.UpdatedAt = now;
                context.TaxGroups.Add(seed);
                createdCount++;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var changed = false;
            if (existing.Name != seed.Name)
            {
                existing.Name = seed.Name;
                changed = true;
            }

            if (existing.Description != seed.Description)
            {
                existing.Description = seed.Description;
                changed = true;
            }

            if (existing.Rate != seed.Rate)
            {
                existing.Rate = seed.Rate;
                changed = true;
            }

            if (existing.IsSystem != seed.IsSystem)
            {
                existing.IsSystem = seed.IsSystem;
                changed = true;
            }

            if (existing.IsDefault != seed.IsDefault)
            {
                existing.IsDefault = seed.IsDefault;
                changed = true;
            }

            if (existing.GroupType != seed.GroupType)
            {
                existing.GroupType = seed.GroupType;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Color) && !string.IsNullOrWhiteSpace(seed.Color))
            {
                existing.Color = seed.Color;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Icon) && !string.IsNullOrWhiteSpace(seed.Icon))
            {
                existing.Icon = seed.Icon;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
                existing.UpdatedBy = "tax-group-seed";
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return createdCount;
    }
}
