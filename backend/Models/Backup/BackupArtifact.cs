using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Describes one produced artifact. Storage path is operational — avoid secrets in this field.
/// </summary>
[Table("backup_artifacts")]
public sealed class BackupArtifact
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("backup_run_id")]
    public Guid BackupRunId { get; set; }

    [ForeignKey(nameof(BackupRunId))]
    public BackupRun? BackupRun { get; set; }

    [Required]
    [Column("artifact_type")]
    public BackupArtifactType ArtifactType { get; set; }

    /// <summary>Opaque descriptor (e.g. relative key); not guaranteed to be a full filesystem path.</summary>
    [MaxLength(1024)]
    [Column("storage_descriptor")]
    public string StorageDescriptor { get; set; } = string.Empty;

    [Column("byte_size")]
    public long? ByteSize { get; set; }

    [MaxLength(64)]
    [Column("content_hash_sha256")]
    public string? ContentHashSha256 { get; set; }

    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    [Required]
    [Column("lifecycle_state")]
    public BackupArtifactLifecycleState LifecycleState { get; set; } = BackupArtifactLifecycleState.Staging;

    /// <summary>
    /// Cost-oriented storage class (Hot/Warm/Cold). Independent of <see cref="LifecycleState"/> pipeline stage.
    /// </summary>
    [Required]
    [Column("storage_tier")]
    public BackupStorageTier StorageTier { get; set; } = BackupStorageTier.Hot;

    /// <summary>UI-safe external locator after archive copy (e.g. archive/runId/file); never a host filesystem path.</summary>
    [MaxLength(512)]
    [Column("external_redacted_locator")]
    public string? ExternalRedactedLocator { get; set; }

    /// <summary>UI-safe cloud object key after cold upload (provider + bucket + key; no secrets).</summary>
    [MaxLength(512)]
    [Column("cloud_locator")]
    public string? CloudLocator { get; set; }

    [MaxLength(32)]
    [Column("cloud_provider")]
    public string? CloudProvider { get; set; }

    [Column("compressed_byte_size")]
    public long? CompressedByteSize { get; set; }

    [Column("deduplicated_from_artifact_id")]
    public Guid? DeduplicatedFromArtifactId { get; set; }

    [Column("moved_to_cold_at_utc")]
    public DateTime? MovedToColdAtUtc { get; set; }

    [Column("immutable_until_utc")]
    public DateTime? ImmutableUntilUtc { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
