namespace KasseAPI_Final.Configuration;

/// <summary>
/// Tenant-scoped quotas for high-risk admin mutations. Bound from <c>TenantOperationLimits</c>.
/// When <see cref="Enabled"/> is false, <see cref="Middleware.OperationLimitMiddleware"/> is a no-op.
/// </summary>
public sealed class TenantOperationLimitsOptions
{
    public const string SectionName = "TenantOperationLimits";

    /// <summary>When false, middleware does not enforce quotas.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true and the host is Development, skip enforcement.
    /// Ignored outside Development (fail-closed when Enabled).
    /// </summary>
    public bool BypassInDevelopment { get; set; } = true;

    /// <summary>Max product units soft-deleted via bulk-deactivate / deactivate-all per tenant per UTC day.</summary>
    public int MaxBulkDeletePerDay { get; set; } = 50;

    /// <summary>Max product update requests (price/stammdaten) per tenant per UTC hour.</summary>
    public int MaxPriceUpdatePerHour { get; set; } = 100;

    /// <summary>Max product create requests per tenant per UTC day.</summary>
    public int MaxProductCreatePerDay { get; set; } = 500;

    /// <summary>Max user create requests per tenant per UTC day.</summary>
    public int MaxUserCreatePerDay { get; set; } = 20;

    /// <summary>Max manual backup triggers per tenant per UTC day.</summary>
    public int MaxBackupPerDay { get; set; } = 5;

    /// <summary>Max product/customer/audit export downloads per tenant per UTC day.</summary>
    public int MaxExportPerDay { get; set; } = 10;

    /// <summary>
    /// Single-request bulk deactivate unit count that requires <c>X-Critical-Action-Approval</c>.
    /// </summary>
    public int RequireApprovalForBulkDelete { get; set; } = 500;

    /// <summary>
    /// Reserved for future bulk price-update endpoints (unit count requiring approval).
    /// Single product PUT always counts as 1 against <see cref="MaxPriceUpdatePerHour"/>.
    /// </summary>
    public int RequireApprovalForPriceUpdate { get; set; } = 200;
}

/// <summary>Quota bucket kinds tracked by <see cref="Services.Operations.IOperationLimitService"/>.</summary>
public enum TenantOperationLimitKind
{
    BulkDelete = 1,
    PriceUpdate = 2,
    ProductCreate = 3,
    UserCreate = 4,
    Backup = 5,
    Export = 6,
}
