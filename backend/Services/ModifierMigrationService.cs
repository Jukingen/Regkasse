using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Phase 2: Legacy modifier → sellable add-on product migration.
    /// Safety model:
    /// - Explicit, operator-controlled: run via API (POST /api/admin/migrate-legacy-modifiers) or CLI (dotnet run -- migrate-legacy-modifiers).
    /// - Does NOT delete original modifier records; production behavior is NOT switched automatically.
    /// - Idempotent: already-migrated modifiers (Product.LegacyModifierId set) are skipped; no duplicate product creation on repeated runs.
    /// - Dry-run: no DB writes; report only. Safe to run in staging first.
    /// TODO Phase 2: Remove legacy modifiers after full migration and cart/receipt simplification.
    /// </summary>
    public class ModifierMigrationService : IModifierMigrationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ModifierMigrationService> _logger;

        public ModifierMigrationService(AppDbContext context, ILogger<ModifierMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ModifierMigrationResultDto> MigrateAsync(Guid defaultCategoryId, bool dryRun = false, CancellationToken cancellationToken = default)
        {
            var result = new ModifierMigrationResultDto();

            var category = await _context.Categories.FindAsync(new object[] { defaultCategoryId }, cancellationToken);
            if (category == null)
            {
                _logger.LogWarning("Modifier migration: Category {CategoryId} not found.", defaultCategoryId);
                result.ErrorCount = 1;
                result.Errors.Add(new ModifierMigrationErrorDto
                {
                    ModifierId = Guid.Empty,
                    ModifierName = "(n/a)",
                    Reason = $"Default category {defaultCategoryId} not found. Create a category first and pass its ID."
                });
                return result;
            }

            var modifiers = await _context.ProductModifiers
                .Where(m => m.IsActive)
                .Include(m => m.ModifierGroup)
                .OrderBy(m => m.ModifierGroupId)
                .ThenBy(m => m.SortOrder)
                .ToListAsync(cancellationToken);

            var alreadyMigratedProductIds = await _context.Products
                .Where(p => p.LegacyModifierId != null)
                .Select(p => p.LegacyModifierId!.Value)
                .ToHashSetAsync(cancellationToken);

            result.TotalProcessed = modifiers.Count;

            foreach (var mod in modifiers)
            {
                if (alreadyMigratedProductIds.Contains(mod.Id))
                {
                    var existingProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.LegacyModifierId == mod.Id, cancellationToken);
                    result.SkippedCount++;
                    result.Skipped.Add(new ModifierMigrationItemDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = existingProduct?.Id,
                        GroupId = mod.ModifierGroupId
                    });
                    _logger.LogInformation("Modifier migration skip (already migrated): {ModifierId} {Name}", mod.Id, mod.Name);
                    continue;
                }

                if (mod.ModifierGroup == null || !mod.ModifierGroup.IsActive)
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ModifierMigrationErrorDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        Reason = "Modifier group not found or inactive."
                    });
                    continue;
                }

                if (dryRun)
                {
                    result.MigratedCount++;
                    result.Migrated.Add(new ModifierMigrationItemDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = null,
                        GroupId = mod.ModifierGroupId
                    });
                    continue;
                }

                try
                {
                    var productId = Guid.NewGuid();
                    var barcode = "ADDON-" + productId.ToString("N")[..12];

                    var product = new Product
                    {
                        Id = productId,
                        Name = mod.Name,
                        Price = Math.Round(mod.Price, 2),
                        TaxType = mod.TaxType,
                        Category = category.Name,
                        CategoryId = category.Id,
                        StockQuantity = 0,
                        MinStockLevel = 0,
                        Unit = "Stk",
                        Barcode = barcode,
                        IsActive = true,
                        IsSellableAddOn = true,
                        TaxRate = TaxTypes.GetTaxRate(mod.TaxType),
                        RksvProductType = RksvProductTypes.Standard,
                        LegacyModifierId = mod.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Products.Add(product);

                    var link = new AddOnGroupProduct
                    {
                        ModifierGroupId = mod.ModifierGroupId,
                        ProductId = productId,
                        SortOrder = mod.SortOrder
                    };
                    _context.AddOnGroupProducts.Add(link);

                    await _context.SaveChangesAsync(cancellationToken);
                    alreadyMigratedProductIds.Add(mod.Id);

                    result.MigratedCount++;
                    result.Migrated.Add(new ModifierMigrationItemDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = productId,
                        GroupId = mod.ModifierGroupId
                    });
                    _logger.LogInformation("Modifier migrated to product: {ModifierId} {Name} -> Product {ProductId}", mod.Id, mod.Name, productId);
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add(new ModifierMigrationErrorDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        Reason = ex.Message
                    });
                    _logger.LogWarning(ex, "Modifier migration failed: {ModifierId} {Name}", mod.Id, mod.Name);
                }
            }

            return result;
        }
    }
}
