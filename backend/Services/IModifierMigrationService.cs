using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Phase 2: Legacy modifier'ları sellable add-on product'lara migrate etme. Idempotent; tekrar çalıştırılınca zaten migrate edilmiş olanlar atlanır.
    /// </summary>
    public interface IModifierMigrationService
    {
        /// <summary>
        /// Tüm aktif legacy modifier'ları inceler; aynı grupta aynı Name+Price ile add-on product varsa atlar (idempotent),
        /// diğerlerini yeni Product + AddOnGroupProduct olarak oluşturur.
        /// </summary>
        /// <param name="defaultCategoryId">Yeni ürünlerin atanacağı kategori (zorunlu).</param>
        /// <param name="dryRun">true ise DB'ye yazılmaz, sadece rapor döner.</param>
        /// <param name="cancellationToken">İptal token.</param>
        Task<ModifierMigrationResultDto> MigrateAsync(Guid defaultCategoryId, bool dryRun = false, CancellationToken cancellationToken = default);
    }
}
