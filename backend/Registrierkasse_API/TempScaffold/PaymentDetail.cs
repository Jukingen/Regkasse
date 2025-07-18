using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class PaymentDetail
{
    public Guid Id { get; set; }

    public int PaymentMethod { get; set; }

    public decimal CashAmount { get; set; }

    public decimal CardAmount { get; set; }

    public string CardType { get; set; } = null!;

    public string CardLastDigits { get; set; } = null!;

    public decimal VoucherAmount { get; set; }

    public string VoucherCode { get; set; } = null!;

    public decimal ChangeAmount { get; set; }

    public string Method { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public DateTime? PaymentDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
