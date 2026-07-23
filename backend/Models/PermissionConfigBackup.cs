using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Lightweight RBAC config snapshot (not Tenant/System pg_dump backup).</summary>
[Table("permission_config_backups")]
public class PermissionConfigBackup
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    /// <summary>Manual | AutoBeforeChange</summary>
    [Required]
    [MaxLength(32)]
    [Column("trigger")]
    public string Trigger { get; set; } = PermissionConfigBackupTriggers.Manual;

    [Required]
    [Column("payload_json", TypeName = "jsonb")]
    public string PayloadJson { get; set; } = "{}";

    [Required]
    [MaxLength(64)]
    [Column("payload_hash")]
    public string PayloadHash { get; set; } = string.Empty;

    [Column("schema_version")]
    public int SchemaVersion { get; set; } = 1;
}

public static class PermissionConfigBackupTriggers
{
    public const string Manual = "Manual";
    public const string AutoBeforeChange = "AutoBeforeChange";
}

[Table("permission_usage_daily")]
public class PermissionUsageDaily
{
    [Key]
    [Column("date")]
    public DateOnly Date { get; set; }

    [Column("total_users")]
    public int TotalUsers { get; set; }

    [Required]
    [Column("payload_json", TypeName = "jsonb")]
    public string PayloadJson { get; set; } = "{}";
}

[Table("permission_config_backup_settings")]
public class PermissionConfigBackupSettings
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1;

    [Column("auto_backup_before_changes")]
    public bool AutoBackupBeforeChanges { get; set; } = true;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
