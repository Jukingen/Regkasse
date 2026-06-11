using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CreateNullbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }

    /// <summary>Optional operator note (stored on payment <c>Notes</c> when short enough).</summary>
    [MaxLength(450)]
    public string? Reason { get; set; }

    /// <summary>When null, true if resolved month is 12 (December = Jahres-Nullbeleg tag for future use).</summary>
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

public sealed class CreateStartbelegResponse
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>RKSV QR payload line when signature present (same shape as receipt DTO).</summary>
    public string QrData { get; set; } = string.Empty;
}

/// <summary>RKSV Monatsbeleg (monthly zero signed receipt; Vienna calendar month).</summary>
public sealed class CreateMonatsbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class CreateMonatsbelegResponse
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string QrData { get; set; } = string.Empty;
}

/// <summary>Returned when a past Vienna calendar month is requested without <c>force=true</c>.</summary>
public sealed class MonatsbelegWarningResponse
{
    public bool RequiresForce { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    /// <summary>Client hint: <c>info</c>, <c>warning</c>, or <c>error</c> based on how far back the target month is.</summary>
    public string Severity { get; set; } = "warning";

    public bool CanForce { get; set; }
    public int MonthDiff { get; set; }
}

/// <summary>RKSV Jahresbeleg (annual zero signed receipt; Vienna calendar year).</summary>
public sealed class CreateJahresbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Vienna calendar year (server allows current year or the immediately preceding year).</summary>
    [Range(2000, 2100)]
    public int Year { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }

    /// <summary>Optional note when issuing before year-end (stored in payment notes; not legal advice).</summary>
    [MaxLength(450)]
    public string? EarlyReason { get; set; }
}

public sealed class CreateJahresbelegResponse
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string QrData { get; set; } = string.Empty;
}

/// <summary>RKSV Schlussbeleg (Endbeleg): final zero signed receipt when permanently decommissioning a cash register.</summary>
public sealed class CreateSchlussbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class CreateSchlussbelegResponse
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string QrData { get; set; } = string.Empty;
}
