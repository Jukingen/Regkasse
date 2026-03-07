namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Phase 2: Legacy modifier → add-on product migration sonuç raporu.
    /// Safety: SkippedCount = duplicate/conflict count (already migrated; no duplicate product created on repeated runs).
    /// </summary>
    public class ModifierMigrationResultDto
    {
        public int TotalProcessed { get; set; }
        public int MigratedCount { get; set; }
        /// <summary>Already migrated (idempotent skip). Same as duplicate/conflict count: no second product created.</summary>
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        /// <summary>Başarıyla migrate edilen: ModifierId, ModifierName, ProductId, GroupId.</summary>
        public List<ModifierMigrationItemDto> Migrated { get; set; } = new();
        /// <summary>Zaten migrate edilmiş (idempotent skip / duplicate): ModifierId, ModifierName, ProductId.</summary>
        public List<ModifierMigrationItemDto> Skipped { get; set; } = new();
        /// <summary>Hata alan modifier'lar: ModifierId, ModifierName, Reason.</summary>
        public List<ModifierMigrationErrorDto> Errors { get; set; } = new();
    }

    public class ModifierMigrationItemDto
    {
        public Guid ModifierId { get; set; }
        public string ModifierName { get; set; } = string.Empty;
        public Guid? ProductId { get; set; }
        public Guid? GroupId { get; set; }
    }

    public class ModifierMigrationErrorDto
    {
        public Guid ModifierId { get; set; }
        public string ModifierName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Phase 2: Migration çağrı parametreleri. Sadece defaultCategoryId zorunlu.
    /// </summary>
    public class ModifierMigrationRequestDto
    {
        /// <summary>Yeni oluşturulan add-on ürünlerin atanacağı kategori (zorunlu).</summary>
        public Guid DefaultCategoryId { get; set; }
        /// <summary>true ise hiç yazma yapılmaz; sadece rapor döner (migrate/skip/error sayıları).</summary>
        public bool DryRun { get; set; }
    }
}
