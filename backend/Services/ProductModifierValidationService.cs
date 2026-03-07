using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Validates product-modifier assignment and returns DB-backed modifier prices for fiscal safety.
    /// LEGACY: PaymentService no longer writes modifiers (Phase 3); this is used only for legacy validation
    /// and by ModifierMigrationService. New add-ons use Product (IsSellableAddOn).
    /// </summary>
    public class ProductModifierValidationService : IProductModifierValidationService
    {
        private readonly AppDbContext _context;

        public ProductModifierValidationService(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetAllowedModifierIdsForProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            var allowedIds = await _context.ProductModifierGroupAssignments
                .AsNoTracking()
                .Where(a => a.ProductId == productId)
                .Select(a => a.ModifierGroupId)
                .ToListAsync(cancellationToken);

            if (allowedIds.Count == 0)
                return Array.Empty<Guid>();

            var modifierIds = await _context.ProductModifiers
                .AsNoTracking()
                .Where(m => allowedIds.Contains(m.ModifierGroupId))
                .Select(m => m.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            return modifierIds;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ModifierPriceDto>> GetAllowedModifiersWithPricesForProductAsync(Guid productId, IReadOnlyList<Guid> requestedModifierIds, CancellationToken cancellationToken = default)
        {
            if (requestedModifierIds == null || requestedModifierIds.Count == 0)
                return Array.Empty<ModifierPriceDto>();

            var allowedIds = await GetAllowedModifierIdsForProductAsync(productId, cancellationToken);
            var allowedSet = allowedIds.ToHashSet();
            var requestedSet = requestedModifierIds.Distinct().ToList();
            var toLoad = requestedSet.Where(id => allowedSet.Contains(id)).ToList();

            if (toLoad.Count == 0)
                return Array.Empty<ModifierPriceDto>();

            var modifiers = await _context.ProductModifiers
                .AsNoTracking()
                .Where(m => toLoad.Contains(m.Id))
                .Select(m => new ModifierPriceDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = Math.Round(m.Price, 2),
                    TaxType = m.TaxType
                })
                .ToListAsync(cancellationToken);

            return modifiers;
        }
    }
}
