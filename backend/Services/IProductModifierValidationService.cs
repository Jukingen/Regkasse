namespace KasseAPI_Final.Services
{
    /// <summary>
    /// RKSV/fiscal: Validates that modifiers are allowed for a product and provides DB-backed prices (no FE trust).
    /// </summary>
    public interface IProductModifierValidationService
    {
        /// <summary>
        /// Returns modifier IDs that belong to any modifier group assigned to the product.
        /// Uses ProductModifierGroupAssignment -> ProductModifierGroup -> Modifiers. AsNoTracking.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetAllowedModifierIdsForProductAsync(Guid productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns Id, Name, Price (2 dp), TaxType for modifiers that are allowed for the given product.
        /// Only modifiers in allowedSet are returned. Use for price lookup and receipt display; never trust FE price.
        /// </summary>
        Task<IReadOnlyList<ModifierPriceDto>> GetAllowedModifiersWithPricesForProductAsync(Guid productId, IReadOnlyList<Guid> requestedModifierIds, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// DTO for modifier price lookup; no EF entities exposed.
    /// </summary>
    public sealed class ModifierPriceDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        /// <summary>Unit price (gross), 2 decimal places.</summary>
        public decimal Price { get; init; }
        public int TaxType { get; init; }
    }
}
