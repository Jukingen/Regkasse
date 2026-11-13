namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Backup product strategy: Mandanten-Admin (tenant-bound) vs Super Admin (system-wide).
/// Data plane today remains one PostgreSQL logical dump; strategy drives ACL metadata,
/// retention defaults, and table-exclude policy (Identity in/out).
/// </summary>
public enum BackupStrategyKind
{
    /// <summary>TenantAdmin / Manager: tenant-scoped access, Identity tables excluded, ~30d retention.</summary>
    Tenant = 0,

    /// <summary>SuperAdmin: deployment-wide access, Identity included, ~90d retention.</summary>
    System = 1
}
