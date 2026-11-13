using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant operational caps (users, registers, catalog, fiscal volume, backups, offline).
/// One row per mandant (<c>UNIQUE(tenant_id)</c>). Missing rows are created with defaults on first read.
/// Assignment is 1:1 on <c>CashRegister.AssignedUserId</c> — there is no per-register user cap.
/// </summary>
[Table("tenant_limits")]
public sealed class TenantLimits : ITenantEntity
{
    public const int DefaultMaxActiveRegistersPerUser = 5;
    public const int DefaultMaxProductsPerTenant = 10000;
    public const int DefaultMaxUsersPerTenant = 50;
    public const int DefaultDailyMaxTransactions = 1000;
    public const decimal DefaultMaxTransactionAmount = 10000m;
    public const decimal DefaultDailyMaxRevenue = 50000m;
    public const int DefaultMaxBackupsPerTenant = 50;
    public const int DefaultMaxBackupSizeMb = 500;
    public const int DefaultMaxOfflineTransactions = 50;

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [JsonIgnore]
    public Tenant? Tenant { get; set; }

    [Column("max_active_registers_per_user")]
    public int MaxActiveRegistersPerUser { get; set; } = DefaultMaxActiveRegistersPerUser;

    [Column("max_products_per_tenant")]
    public int MaxProductsPerTenant { get; set; } = DefaultMaxProductsPerTenant;

    [Column("max_users_per_tenant")]
    public int MaxUsersPerTenant { get; set; } = DefaultMaxUsersPerTenant;

    [Column("daily_max_transactions")]
    public int DailyMaxTransactions { get; set; } = DefaultDailyMaxTransactions;

    [Column("max_transaction_amount", TypeName = "decimal(18,2)")]
    public decimal MaxTransactionAmount { get; set; } = DefaultMaxTransactionAmount;

    [Column("daily_max_revenue", TypeName = "decimal(18,2)")]
    public decimal DailyMaxRevenue { get; set; } = DefaultDailyMaxRevenue;

    [Column("max_backups_per_tenant")]
    public int MaxBackupsPerTenant { get; set; } = DefaultMaxBackupsPerTenant;

    [Column("max_backup_size_mb")]
    public int MaxBackupSizeMb { get; set; } = DefaultMaxBackupSizeMb;

    [Column("max_offline_transactions")]
    public int MaxOfflineTransactions { get; set; } = DefaultMaxOfflineTransactions;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static TenantLimits CreateDefault(Guid tenantId)
    {
        var row = new TenantLimits { TenantId = tenantId };
        row.ApplyDefaults();
        return row;
    }

    public void ApplyDefaults()
    {
        MaxActiveRegistersPerUser = DefaultMaxActiveRegistersPerUser;
        MaxProductsPerTenant = DefaultMaxProductsPerTenant;
        MaxUsersPerTenant = DefaultMaxUsersPerTenant;
        DailyMaxTransactions = DefaultDailyMaxTransactions;
        MaxTransactionAmount = DefaultMaxTransactionAmount;
        DailyMaxRevenue = DefaultDailyMaxRevenue;
        MaxBackupsPerTenant = DefaultMaxBackupsPerTenant;
        MaxBackupSizeMb = DefaultMaxBackupSizeMb;
        MaxOfflineTransactions = DefaultMaxOfflineTransactions;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Integer projection of a named cap. Money limits are truncated to whole units
    /// for <c>GetLimitValueAsync</c> / <c>CheckLimitAsync</c>.
    /// </summary>
    public int GetIntLimit(string limitKey) =>
        NormalizeLimitKey(limitKey) switch
        {
            TenantLimitKeys.MaxActiveRegistersPerUser => MaxActiveRegistersPerUser,
            TenantLimitKeys.MaxProductsPerTenant => MaxProductsPerTenant,
            TenantLimitKeys.MaxUsersPerTenant => MaxUsersPerTenant,
            TenantLimitKeys.DailyMaxTransactions => DailyMaxTransactions,
            TenantLimitKeys.MaxTransactionAmount => decimal.ToInt32(decimal.Truncate(MaxTransactionAmount)),
            TenantLimitKeys.DailyMaxRevenue => decimal.ToInt32(decimal.Truncate(DailyMaxRevenue)),
            TenantLimitKeys.MaxBackupsPerTenant => MaxBackupsPerTenant,
            TenantLimitKeys.MaxBackupSizeMb => MaxBackupSizeMb,
            TenantLimitKeys.MaxOfflineTransactions => MaxOfflineTransactions,
            _ => throw new ArgumentOutOfRangeException(nameof(limitKey), limitKey, "Unknown tenant limit key."),
        };

    public static string NormalizeLimitKey(string limitKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(limitKey);
        return limitKey.Trim() switch
        {
            "max_active_registers_per_user" => TenantLimitKeys.MaxActiveRegistersPerUser,
            "max_products_per_tenant" => TenantLimitKeys.MaxProductsPerTenant,
            "max_users_per_tenant" => TenantLimitKeys.MaxUsersPerTenant,
            "daily_max_transactions" => TenantLimitKeys.DailyMaxTransactions,
            "max_transaction_amount" => TenantLimitKeys.MaxTransactionAmount,
            "daily_max_revenue" => TenantLimitKeys.DailyMaxRevenue,
            "max_backups_per_tenant" => TenantLimitKeys.MaxBackupsPerTenant,
            "max_backup_size_mb" => TenantLimitKeys.MaxBackupSizeMb,
            "max_offline_transactions" => TenantLimitKeys.MaxOfflineTransactions,
            var key => key,
        };
    }
}

/// <summary>Canonical camelCase keys for <see cref="TenantLimits"/> lookups (API + FA).</summary>
public static class TenantLimitKeys
{
    public const string MaxActiveRegistersPerUser = "maxActiveRegistersPerUser";
    public const string MaxProductsPerTenant = "maxProductsPerTenant";
    public const string MaxUsersPerTenant = "maxUsersPerTenant";
    public const string DailyMaxTransactions = "dailyMaxTransactions";
    public const string MaxTransactionAmount = "maxTransactionAmount";
    public const string DailyMaxRevenue = "dailyMaxRevenue";
    public const string MaxBackupsPerTenant = "maxBackupsPerTenant";
    public const string MaxBackupSizeMb = "maxBackupSizeMB";
    public const string MaxOfflineTransactions = "maxOfflineTransactions";

    public static readonly string[] All =
    [
        MaxActiveRegistersPerUser,
        MaxProductsPerTenant,
        MaxUsersPerTenant,
        DailyMaxTransactions,
        MaxTransactionAmount,
        DailyMaxRevenue,
        MaxBackupsPerTenant,
        MaxBackupSizeMb,
        MaxOfflineTransactions,
    ];
}
