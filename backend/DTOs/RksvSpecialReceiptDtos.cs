using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CreateNullbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    /// <summary>Optional operator note (stored on payment <c>Notes</c> when short enough).</summary>
    [MaxLength(450)]
    public string? Reason { get; set; }

    /// <summary>When null, true if <see cref="Month"/> is 12 (December = Jahres-Nullbeleg tag for future use).</summary>
    public bool? ActsAsJahresbeleg { get; set; }
}

public sealed class CreateNullbelegResponse
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public bool ActsAsJahresbeleg { get; set; }
}
