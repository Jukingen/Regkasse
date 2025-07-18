using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Transaction
{
    public Guid Id { get; set; }

    public string TransactionNumber { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? Reference { get; set; }

    public string Status { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? ReceiptId { get; set; }

    public Guid? InvoiceId { get; set; }

    public string? CashRegisterId { get; set; }

    public string? UserId { get; set; }

    public Guid? CashRegisterId1 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual CashRegister? CashRegisterId1Navigation { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual Receipt? Receipt { get; set; }

    public virtual AspNetUser? User { get; set; }
}
