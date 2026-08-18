namespace KasseAPI_Final.Services.Caching;

/// <summary>
/// Canonical cache key templates for domain <see cref="ICacheService"/> entries.
/// Prefer <see cref="Format"/> with these constants over inline magic strings.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Mandant / billing license status snapshot (Cache-Aside).
    /// Format: <c>license_status_{tenantId}</c> — arg0 = <see cref="Guid"/> tenant id.
    /// </summary>
    public const string LicenseStatus = "license_status_{0}";

    /// <summary>
    /// Prefix shared by all license status keys (prefix clear / ops).
    /// </summary>
    public const string LicenseStatusPrefix = "license_status_";

    /// <summary>Per-key lookup cache. Format: <c>license_key_{normalizedKey}</c>.</summary>
    public const string LicenseKeyLookup = "license_key_{0}";

    /// <summary>Tenant license overlay. Format: <c>license_tenant_{tenantId}</c>.</summary>
    public const string LicenseTenant = "license_tenant_{0}";

    /// <summary>Admin issued-license list cache (fixed string).</summary>
    public const string LicenseAdminList = "license_admin_list";

    /// <summary>Admin billing sales list cache (fixed string).</summary>
    public const string LicenseBillingSales = "license_billing_sales";

    /// <summary>
    /// Active product list for a tenant (unfiltered).
    /// Format: <c>product_list_{tenantId}</c> — arg0 = tenant id.
    /// Also used as the prefix for category-filtered variants (<see cref="ProductListByCategory"/>).
    /// </summary>
    public const string ProductList = "product_list_{0}";

    /// <summary>
    /// Category-filtered product list for a tenant.
    /// Format: <c>product_list_{tenantId}_cat_{categoryId}</c> — arg0 = tenant id, arg1 = category id.
    /// </summary>
    public const string ProductListByCategory = "product_list_{0}_cat_{1}";

    /// <summary>
    /// Optional single-product projection.
    /// Format: <c>product_detail_{productId}</c> — arg0 = product id.
    /// </summary>
    public const string ProductDetail = "product_detail_{0}";

    /// <summary>
    /// Effective permission snapshot for a user (invalidated on role change).
    /// Format: <c>user_permissions_{userId}</c> — arg0 = Identity user id string.
    /// </summary>
    public const string UserPermissions = "user_permissions_{0}";

    /// <summary>
    /// Tenant settings snapshot.
    /// Format: <c>tenant_settings_{tenantId}</c> — arg0 = tenant id.
    /// </summary>
    public const string TenantSettings = "tenant_settings_{0}";

    /// <summary>
    /// Cached TSE health snapshot key template (device/register scoped when used).
    /// Format: <c>tse_health_{0}</c> — arg0 = cash register id or device scope id.
    /// Reserved for domain <see cref="ICacheService"/>; process TSE monitor may use in-memory snapshots instead.
    /// </summary>
    public const string TseHealth = "tse_health_{0}";

    /// <summary>
    /// Ready/deps probe ping key (not business data). Fixed string — do not <see cref="Format"/>.
    /// </summary>
    public const string HealthPing = "health_check_ping";

    /// <summary>
    /// Super Admin customer analytics snapshot. Fixed string — do not <see cref="Format"/>.
    /// </summary>
    public const string CustomerAnalytics = "admin_customer_analytics";

    /// <summary>
    /// Super Admin TSE usage snapshot.
    /// Format: <c>admin_tse_usage_analytics_{from}_{to}</c> — arg0/arg1 = yyyyMMdd UTC bounds.
    /// </summary>
    public const string TseUsageAnalytics = "admin_tse_usage_analytics_{0}_{1}";

    /// <summary>
    /// Super Admin POS payment-volume snapshot.
    /// Format: <c>admin_payment_volume_analytics_{groupBy}_{from}_{to}</c>.
    /// </summary>
    public const string PaymentVolumeAnalytics = "admin_payment_volume_analytics_{0}_{1}_{2}";

    /// <summary>
    /// Formats a template from this class (e.g. <see cref="LicenseStatus"/>) with the given arguments.
    /// </summary>
    public static string Format(string key, params object[] args) =>
        string.Format(key, args);
}
