using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class DailyReport
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    public DateTime ReportDate { get; set; }

    public DateTime ReportTime { get; set; }

    public string TseSerialNumber { get; set; } = null!;

    public DateTime TseTime { get; set; }

    public string TseProcessType { get; set; } = null!;

    public int TotalInvoices { get; set; }

    public int TotalTransactions { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public decimal CashAmount { get; set; }

    public decimal CardAmount { get; set; }

    public decimal VoucherAmount { get; set; }

    public decimal StandardTaxAmount { get; set; }

    public decimal ReducedTaxAmount { get; set; }

    public decimal SpecialTaxAmount { get; set; }

    public string Status { get; set; } = null!;

    public decimal TotalSales { get; set; }

    public decimal CashPayments { get; set; }

    public decimal CardPayments { get; set; }

    public string TseSignature { get; set; } = null!;

    public string KassenId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
