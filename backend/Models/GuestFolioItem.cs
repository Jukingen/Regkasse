using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("guest_folio_items")]
public sealed class GuestFolioItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("folio_id")]
    public Guid FolioId { get; set; }

    public GuestFolio? Folio { get; set; }

    [Column("payment_detail_id")]
    public Guid? PaymentDetailId { get; set; }

    public PaymentDetails? PaymentDetail { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
