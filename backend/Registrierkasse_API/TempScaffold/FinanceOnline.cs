using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class FinanceOnline
{
    public Guid Id { get; set; }

    public string TransactionNumber { get; set; } = null!;

    public Guid InvoiceId { get; set; }

    public string SignatureCertificate { get; set; } = null!;

    public string SignatureValue { get; set; } = null!;

    public string QrCode { get; set; } = null!;

    public string ResponseCode { get; set; } = null!;

    public string ResponseMessage { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
