namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Phase 2: Legacy modifier batch migration result. Best-effort: partial success is by design.
    /// Use Migrated / Skipped / Errors as the source of truth; failed items do not roll back successful ones.
    /// </summary>
    public class ModifierMigrationResultDto
    {
        public int TotalProcessed { get; set; }
        public int MigratedCount { get; set; }
        /// <summary>Already migrated (idempotent skip). Same as duplicate/conflict count: no second product created.</summary>
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        /// <summary>Successfully migrated in this run: ModifierId, ModifierName, ProductId, GroupId.</summary>
        public List<ModifierMigrationItemDto> Migrated { get; set; } = new();
        /// <summary>Already migrated in a previous run (idempotent skip): ModifierId, ModifierName, ProductId.</summary>
        public List<ModifierMigrationItemDto> Skipped { get; set; } = new();
        /// <summary>Failed in this run (e.g. inactive group, DB error). Modifier remains active; no product created.</summary>
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

    /// <summary>
    /// Single legacy modifier migration request (FE-Admin "Als Produkt migrieren").
    /// </summary>
    public class MigrateSingleModifierRequestDto
    {
        /// <summary>Kategorie für das neue Add-on-Produkt (erforderlich).</summary>
        public Guid CategoryId { get; set; }
        /// <summary>Legacy-Modifier nach Migration deaktivieren (IsActive=false). Default: true.</summary>
        public bool MarkModifierInactive { get; set; } = true;
    }

    /// <summary>
    /// Single modifier migration result.
    /// </summary>
    public class MigrateSingleModifierResultDto
    {
        public Guid ModifierId { get; set; }
        public string ModifierName { get; set; } = string.Empty;
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public Guid GroupId { get; set; }
        /// <summary>true = bereits migriert (idempotent skip).</summary>
        public bool AlreadyMigrated { get; set; }
        /// <summary>true = Legacy-Modifier wurde deaktiviert.</summary>
        public bool ModifierMarkedInactive { get; set; }
    }

    /// <summary>
    /// Phase 2: Legacy modifier migration progress. Operators use this to measure Phase B completion.
    /// </summary>
    public class LegacyModifierMigrationProgressDto
    {
        /// <summary>Count of product_modifiers where is_active = true (unmigrated active legacy modifiers).</summary>
        public int ActiveLegacyModifiersCount { get; set; }

        /// <summary>Count of active modifier groups that have zero active add-on products and at least one active legacy modifier (fallback groups).</summary>
        public int GroupsWithModifiersOnlyCount { get; set; }
    }
}
