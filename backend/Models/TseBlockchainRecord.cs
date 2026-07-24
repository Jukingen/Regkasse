using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Optional off-chain / simulated blockchain anchor of an existing TSE signature hash.
/// Does NOT replace RKSV compact JWS or the fiscal signature chain.
/// </summary>
[Table("tse_blockchain_records")]
public sealed class TseBlockchainRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Optional link to payment / receipt / device context (opaque).</summary>
    [MaxLength(64)]
    [Column("source_type")]
    public string SourceType { get; set; } = "Signature";

    [Column("source_id")]
    public Guid? SourceId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("transaction_hash")]
    public string TransactionHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("block_hash")]
    public string BlockHash { get; set; } = string.Empty;

    [Column("block_number")]
    public long BlockNumber { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 hex of the anchored signature payload (never stores full JWS secrets when redacted).</summary>
    [Required]
    [MaxLength(128)]
    [Column("signature_hash")]
    public string SignatureHash { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("signature_preview")]
    public string? SignaturePreview { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Column("is_simulated")]
    public bool IsSimulated { get; set; } = true;

    [MaxLength(64)]
    [Column("network_name")]
    public string NetworkName { get; set; } = "regkasse-sim";

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}

/// <summary>Singleton simulated ledger tip for deterministic block numbers.</summary>
[Table("tse_blockchain_ledger_state")]
public sealed class TseBlockchainLedgerState
{
    public static readonly Guid SingletonId = Guid.Parse("b10ccba1-0000-4000-8000-000000000001");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    [Column("current_block_number")]
    public long CurrentBlockNumber { get; set; }

    [MaxLength(128)]
    [Column("tip_block_hash")]
    public string TipBlockHash { get; set; } = "0".PadLeft(64, '0');

    [MaxLength(64)]
    [Column("network_name")]
    public string NetworkName { get; set; } = "regkasse-sim";

    [Column("is_connected")]
    public bool IsConnected { get; set; } = true;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("total_transactions")]
    public long TotalTransactions { get; set; }
}
