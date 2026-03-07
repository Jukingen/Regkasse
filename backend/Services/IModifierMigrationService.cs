using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Phase 2: Legacy modifier'ları sellable add-on product'lara migrate etme. Idempotent; tekrar çalıştırılınca zaten migrate edilmiş olanlar atlanır.
    /// </summary>
    public interface IModifierMigrationService
    {
        /// <summary>
        /// Batch migration (best-effort). Processes all active legacy modifiers; already-migrated are skipped (idempotent).
        /// Failures do not roll back successes; result.Migrated and result.Errors report outcome. Batch does not deactivate modifiers.
        /// </summary>
        /// <param name="defaultCategoryId">Yeni ürünlerin atanacağı kategori (zorunlu).</param>
        /// <param name="dryRun">true ise DB'ye yazılmaz, sadece rapor döner.</param>
        /// <param name="cancellationToken">İptal token.</param>
        Task<ModifierMigrationResultDto> MigrateAsync(Guid defaultCategoryId, bool dryRun = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Einzelnen Legacy-Modifier als Add-on-Produkt migrieren (FE-Admin "Als Produkt migrieren").
        /// Idempotent: bereits migriert = Skip, kein zweites Produkt.
        /// </summary>
        /// <param name="modifierId">Zu migrierender Modifier.</param>
        /// <param name="groupId">Gruppe (zur Validierung: Modifier muss zu dieser Gruppe gehören).</param>
        /// <param name="categoryId">Kategorie für das neue Produkt.</param>
        /// <param name="markModifierInactive">Legacy-Modifier nach Migration deaktivieren.</param>
        /// <param name="cancellationToken">İptal token.</param>
        Task<MigrateSingleModifierResultDto> MigrateSingleAsync(Guid modifierId, Guid groupId, Guid categoryId, bool markModifierInactive = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Einzelnen Legacy-Modifier per ModifierId migrieren (Admin: POST /api/admin/modifiers/{modifierId}/migrate-to-product).
        /// Gruppe wird aus Modifier.ModifierGroupId ermittelt. Transaktional; verhindert Duplikate bei inaktiven Modifiern.
        /// </summary>
        /// <param name="modifierId">Zu migrierender Modifier.</param>
        /// <param name="categoryId">Kategorie für das neue Add-on-Produkt.</param>
        /// <param name="markModifierInactive">Legacy-Modifier nach Migration deaktivieren (IsActive=false).</param>
        /// <param name="cancellationToken">İptal token.</param>
        /// <returns>MigrateSingleModifierResultDto mit ProductId, AlreadyMigrated, ModifierMarkedInactive.</returns>
        /// <exception cref="InvalidOperationException">Modifier nicht gefunden, Kategorie ungültig, Modifier bereits inaktiv ohne passendes Produkt.</exception>
        Task<MigrateSingleModifierResultDto> MigrateSingleByModifierIdAsync(Guid modifierId, Guid categoryId, bool markModifierInactive = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Migration progress for Phase B: active legacy modifier count and groups with modifiers only (zero active add-on products).
        /// </summary>
        Task<LegacyModifierMigrationProgressDto> GetMigrationProgressAsync(CancellationToken cancellationToken = default);
    }
}
