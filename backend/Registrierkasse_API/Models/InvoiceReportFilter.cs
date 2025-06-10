using System;

namespace Registrierkasse.Models
{
    public class InvoiceReportFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CustomerId { get; set; }
        public string? PaymentStatus { get; set; }
        public string? InvoiceStatus { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string? SearchQuery { get; set; }
    }

    public class EmailInvoiceRequest
    {
        public string Email { get; set; } = string.Empty;
        public string? TemplateId { get; set; }
    }

    public class PrintInvoiceRequest
    {
        public string? TemplateId { get; set; }
        public string? PrinterName { get; set; }
        public int Copies { get; set; } = 1;
    }

    public class PdfInvoiceRequest
    {
        public string? TemplateId { get; set; }
        public bool IncludeLogo { get; set; } = true;
        public bool IncludeQRCode { get; set; } = true;
    }
} 