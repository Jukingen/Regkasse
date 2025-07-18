using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class DailyReport : BaseEntity
    {
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
        public DateTime ReportTime { get; set; } = DateTime.UtcNow;
        
        public string TseSerialNumber { get; set; } = string.Empty;
        public DateTime TseTime { get; set; }
        public string TseProcessType { get; set; } = string.Empty;
        
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
        
        public string Status { get; set; } = "open"; // open, closed, submitted
        
        public decimal TotalSales { get; set; }
        public decimal CashPayments { get; set; }
        public decimal CardPayments { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string KassenId { get; set; } = string.Empty;
    }
} 
