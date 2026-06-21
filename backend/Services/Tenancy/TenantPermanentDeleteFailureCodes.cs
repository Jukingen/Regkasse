namespace KasseAPI_Final.Services.Tenancy;

/// <summary>Stable API codes for permanent tenant delete failures (FA modal / i18n mapping).</summary>
public static class TenantPermanentDeleteFailureCodes
{
    public const string TenantNotFound = "tenant_not_found";
    public const string LegacyDefaultTenant = "legacy_default_tenant";
    public const string ProductionPolicy = "production_policy";
    public const string NotSoftDeleted = "tenant_not_soft_deleted";
    public const string ConfirmSlugMismatch = "confirm_slug_mismatch";
    public const string CashRegistersPresent = "cash_registers_present";
    public const string FiscalFootprintPresent = "fiscal_footprint_present";
    public const string ForceDeleteDevelopmentOnly = "force_delete_development_only";
    public const string RemainingDependencies = "remaining_dependencies";

    /// <summary>Legacy alias; prefer <see cref="ProductionPolicy"/>.</summary>
    public const string ProductionDisabled = ProductionPolicy;

    /// <summary>Legacy alias; prefer <see cref="FiscalFootprintPresent"/>.</summary>
    public const string FiscalFootprint = FiscalFootprintPresent;

    /// <summary>Legacy alias; covered by <see cref="FiscalFootprintPresent"/> when payments &gt; 0.</summary>
    public const string FiscalPaymentsPresent = FiscalFootprintPresent;
}
