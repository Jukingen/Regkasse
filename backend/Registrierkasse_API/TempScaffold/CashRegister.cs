using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CashRegister
{
    public Guid Id { get; set; }

    public string RegisterNumber { get; set; } = null!;

    public string TseId { get; set; } = null!;

    public string KassenId { get; set; } = null!;

    public string Location { get; set; } = null!;

    public decimal StartingBalance { get; set; }

    public decimal CurrentBalance { get; set; }

    public DateTime? LastBalanceUpdate { get; set; }

    public int Status { get; set; }

    public string? CurrentUserId { get; set; }

    public DateTime? LastClosingDate { get; set; }

    public decimal LastClosingAmount { get; set; }

    public string Notes { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CashRegisterTransaction> CashRegisterTransactions { get; set; } = new List<CashRegisterTransaction>();

    public virtual AspNetUser? CurrentUser { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
