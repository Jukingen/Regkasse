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
    /// - Idempotent: already-migrated = add-on product in same group with same Name+Price; skipped on repeated runs.
    /// - Dry-run: no DB writes; report only. Safe to run in staging first.
    /// - Batch (MigrateAsync): Best-effort. Each modifier is attempted; failures do not roll back previous successes.
    ///   Result DTO reports Migrated, Skipped, and Errors. Batch never deactivates legacy modifiers (unlike single migration).
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

        /// <summary>
        /// Batch migration: best-effort. Each active modifier is attempted; on failure the item is reported in result.Errors
        /// and processing continues. Successful migrations are committed per item; no transaction wraps the whole batch.
        /// Legacy modifiers are never deactivated by this method. Use result.Migrated, result.Skipped, result.Errors for operational reporting.
        /// </summary>
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

            result.TotalProcessed = modifiers.Count;

            foreach (var mod in modifiers)
            {
                // Idempotency: already migrated = add-on product in same group with same Name+Price (no legacy_modifier_id column).
                var existingProduct = await _context.AddOnGroupProducts
                    .Where(a => a.ModifierGroupId == mod.ModifierGroupId)
                    .Where(a => a.Product != null && a.Product.IsSellableAddOn && a.Product.Name == mod.Name && a.Product.Price == Math.Round(mod.Price, 2))
                    .Select(a => a.Product)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingProduct != null)
                {
                    result.SkippedCount++;
                    result.Skipped.Add(new ModifierMigrationItemDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = existingProduct.Id,
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
                    var product = CreateAddOnProductFromModifier(mod, category);
                    _context.Products.Add(product);

                    var link = new AddOnGroupProduct
                    {
                        ModifierGroupId = mod.ModifierGroupId,
                        ProductId = product.Id,
                        SortOrder = mod.SortOrder
                    };
                    _context.AddOnGroupProducts.Add(link);

                    await _context.SaveChangesAsync(cancellationToken);

                    result.MigratedCount++;
                    result.Migrated.Add(new ModifierMigrationItemDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = product.Id,
                        GroupId = mod.ModifierGroupId
                    });
                    _logger.LogInformation("Modifier migrated to product: {ModifierId} {Name} -> Product {ProductId}", mod.Id, mod.Name, product.Id);
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

        /// <inheritdoc />
        public async Task<MigrateSingleModifierResultDto> MigrateSingleAsync(Guid modifierId, Guid groupId, Guid categoryId, bool markModifierInactive = true, CancellationToken cancellationToken = default)
        {
            var mod = await _context.ProductModifiers
                .Include(m => m.ModifierGroup)
                .FirstOrDefaultAsync(m => m.Id == modifierId, cancellationToken);
            if (mod == null)
                throw new InvalidOperationException($"Modifier {modifierId} not found.");
            if (mod.ModifierGroupId != groupId)
                throw new InvalidOperationException($"Modifier {modifierId} does not belong to group {groupId}.");

            var category = await _context.Categories.FindAsync(new object[] { categoryId }, cancellationToken);
            if (category == null)
                throw new InvalidOperationException($"Category {categoryId} not found.");

            // Idempotency: already migrated = add-on product in same group with same Name+Price
            var existingProduct = await _context.AddOnGroupProducts
                .Where(a => a.ModifierGroupId == mod.ModifierGroupId)
                .Where(a => a.Product != null && a.Product.IsSellableAddOn && a.Product.Name == mod.Name && a.Product.Price == Math.Round(mod.Price, 2))
                .Select(a => a.Product)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingProduct != null)
            {
                var wasInactive = !mod.IsActive;
                if (markModifierInactive && mod.IsActive)
                {
                    mod.IsActive = false;
                    mod.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
                return new MigrateSingleModifierResultDto
                {
                    ModifierId = mod.Id,
                    ModifierName = mod.Name,
                    ProductId = existingProduct.Id,
                    ProductName = existingProduct.Name,
                    GroupId = mod.ModifierGroupId,
                    AlreadyMigrated = true,
                    ModifierMarkedInactive = !wasInactive && !mod.IsActive
                };
            }

            if (mod.ModifierGroup == null || !mod.ModifierGroup.IsActive)
                throw new InvalidOperationException("Modifier group not found or inactive.");

            var productId = Guid.NewGuid();
            var barcode = "ADDON-" + productId.ToString("N")[..12];
            var product = CreateAddOnProductFromModifier(mod, category, productId, barcode);
            _context.Products.Add(product);

            var link = new AddOnGroupProduct
            {
                ModifierGroupId = mod.ModifierGroupId,
                ProductId = productId,
                SortOrder = mod.SortOrder
            };
            _context.AddOnGroupProducts.Add(link);

            if (markModifierInactive)
            {
                mod.IsActive = false;
                mod.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Modifier migrated to product: {ModifierId} {Name} -> Product {ProductId}", mod.Id, mod.Name, productId);

            return new MigrateSingleModifierResultDto
            {
                ModifierId = mod.Id,
                ModifierName = mod.Name,
                ProductId = productId,
                ProductName = product.Name,
                GroupId = mod.ModifierGroupId,
                AlreadyMigrated = false,
                ModifierMarkedInactive = markModifierInactive
            };
        }

        /// <inheritdoc />
        public async Task<MigrateSingleModifierResultDto> MigrateSingleByModifierIdAsync(Guid modifierId, Guid categoryId, bool markModifierInactive = true, CancellationToken cancellationToken = default)
        {
            var mod = await _context.ProductModifiers
                .Include(m => m.ModifierGroup)
                .FirstOrDefaultAsync(m => m.Id == modifierId, cancellationToken);
            if (mod == null)
                throw new InvalidOperationException($"Modifier {modifierId} not found.");

            var category = await _context.Categories.FindAsync(new object[] { categoryId }, cancellationToken);
            if (category == null)
                throw new InvalidOperationException($"Category {categoryId} not found.");

            // Inactive modifier: already migrated. Check for existing product; if found return idempotent result.
            if (!mod.IsActive)
            {
                var existingProduct = await _context.AddOnGroupProducts
                    .Where(a => a.ModifierGroupId == mod.ModifierGroupId)
                    .Where(a => a.Product != null && a.Product.IsSellableAddOn && a.Product.Name == mod.Name && a.Product.Price == Math.Round(mod.Price, 2))
                    .Select(a => a.Product)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingProduct != null)
                {
                    return new MigrateSingleModifierResultDto
                    {
                        ModifierId = mod.Id,
                        ModifierName = mod.Name,
                        ProductId = existingProduct.Id,
                        ProductName = existingProduct.Name,
                        GroupId = mod.ModifierGroupId,
                        AlreadyMigrated = true,
                        ModifierMarkedInactive = false
                    };
                }
                throw new InvalidOperationException($"Modifier {modifierId} is already inactive (already migrated). No matching add-on product found in group.");
            }

            // Transactional: ensures atomicity for production (PostgreSQL). InMemory ignores transactions (ConfigureWarnings in tests).
            if (_context.Database.IsRelational())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await MigrateSingleAsync(modifierId, mod.ModifierGroupId, categoryId, markModifierInactive, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            return await MigrateSingleAsync(modifierId, mod.ModifierGroupId, categoryId, markModifierInactive, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<LegacyModifierMigrationProgressDto> GetMigrationProgressAsync(CancellationToken cancellationToken = default)
        {
            // Active legacy modifiers: product_modifiers where is_active = true
            var activeLegacyModifiersCount = await _context.ProductModifiers
                .AsNoTracking()
                .CountAsync(m => m.IsActive, cancellationToken);

            // Groups with modifiers only: active groups with at least one active legacy modifier and zero active add-on products
            var groupsWithModifiersOnlyCount = await _context.ProductModifierGroups
                .AsNoTracking()
                .Where(g => g.IsActive)
                .Where(g => _context.ProductModifiers.Any(m => m.ModifierGroupId == g.Id && m.IsActive))
                .Where(g => !_context.AddOnGroupProducts.Any(a => a.ModifierGroupId == g.Id && a.Product != null && a.Product.IsActive))
                .CountAsync(cancellationToken);

            return new LegacyModifierMigrationProgressDto
            {
                ActiveLegacyModifiersCount = activeLegacyModifiersCount,
                GroupsWithModifiersOnlyCount = groupsWithModifiersOnlyCount
            };
        }

        /// <summary>
        /// Creates a fully populated add-on Product from a legacy modifier. All DB-required fields are set explicitly.
        /// Production-safe: Description is never null (products.description NOT NULL in some DBs).
        /// </summary>
        private static Product CreateAddOnProductFromModifier(ProductModifier mod, Category category)
        {
            var productId = Guid.NewGuid();
            var barcode = "ADDON-" + productId.ToString("N")[..12];
            return CreateAddOnProductFromModifier(mod, category, productId, barcode);
        }

        private static Product CreateAddOnProductFromModifier(ProductModifier mod, Category category, Guid productId, string barcode)
        {
            var name = mod.Name ?? string.Empty;
            return new Product
            {
                Id = productId,
                Name = name,
                Description = name,
                Price = Math.Round(mod.Price, 2),
                TaxType = mod.TaxType,
                Category = category.Name,
                CategoryId = category.Id,
                StockQuantity = 0,
                MinStockLevel = 0,
                Unit = "Stk",
                Barcode = barcode,
                Cost = 0,
                IsActive = true,
                IsSellableAddOn = true,
                TaxRate = TaxTypes.GetTaxRate(mod.TaxType),
                RksvProductType = RksvProductTypes.Standard,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
