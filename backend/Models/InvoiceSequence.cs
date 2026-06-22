using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin license billing invoice sequence counter per calendar month.
/// Used by <see cref="Services.Billing.InvoiceNumberGenerator"/> for atomic sequence allocation.
/// </summary>
[Table("invoice_sequences")]
public class InvoiceSequence
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("year")]
    public int Year { get; set; }

    [Column("month")]
    public int Month { get; set; }

    [Column("last_sequence")]
    public int LastSequence { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
