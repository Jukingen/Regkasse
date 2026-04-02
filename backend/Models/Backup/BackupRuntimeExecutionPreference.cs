using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Tek satırlık kalıcı tercih: admin yedek çalıştırma modu (Fake / PgDump / yapılandırmayı izle).
/// </summary>
[Table("backup_runtime_execution_preferences")]
public sealed class BackupRuntimeExecutionPreference
{
    public const int SingletonId = 1;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = SingletonId;

    public AdminBackupRuntimeExecutionMode Mode { get; set; } = AdminBackupRuntimeExecutionMode.InheritFromConfiguration;

    public DateTime UpdatedAtUtc { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }
}
