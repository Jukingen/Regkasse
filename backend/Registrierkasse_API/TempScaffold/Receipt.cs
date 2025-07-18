using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Receipt
{
    public Guid Id { get; set; }

    public string ReceiptNumber { get; set; } = null!;

    public DateTime ReceiptDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Subtotal { get; set; }

    public string? TseSignature { get; set; }

    public string? KassenId { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public bool IsPrinted { get; set; }

    public bool IsCancelled { get; set; }

    public string? CancellationReason { get; set; }

    public string? CashRegisterId { get; set; }

    public string? UserId { get; set; }

    public Guid? CashRegisterId1 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual CashRegister? CashRegisterId1Navigation { get; set; }

    public virtual ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual AspNetUser? User { get; set; }
}
