using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Encrypted TSE disaster-recovery snapshot for a tenant (devices + failover roles + signature chain + BelegNr sequences).
/// Vendor private keys / fiskaly SCU secrets stay outside; device ApiKey/ApiSecret ciphertext may be included when present.
/// </summary>
[Table("tse_backups")]
public sealed class TseBackupRecord : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    /// <summary>Encrypted JSON payload (<see cref="DTOs.TseBackupPayloadDto"/>).</summary>
    [Required]
    [Column("payload")]
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary><c>BackupAesGcm</c> or <c>DataProtection</c>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("encryption_kind")]
    public string EncryptionKind { get; set; } = "DataProtection";

    [Required]
    [Column("device_count")]
    public int DeviceCount { get; set; }

    [Required]
    [Column("chain_count")]
    public int ChainCount { get; set; }

    [Required]
    [Column("receipt_sequence_count")]
    public int ReceiptSequenceCount { get; set; }

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Schema version of the JSON payload (v2 = full failover registry).</summary>
    [Required]
    [Column("schema_version")]
    public int SchemaVersion { get; set; } = 2;

    [MaxLength(256)]
    [Column("notes")]
    public string? Notes { get; set; }
}
