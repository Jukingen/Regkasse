using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-register monotonic Belegnummer counter for DE KassenSicherheit.
/// Does not reset daily. Not the Austrian <c>receipt_sequences</c> table.
/// </summary>
[Table("de_receipt_sequences")]
public class DeReceiptSequence
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [Column("next_sequence")]
    public int NextSequence { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
