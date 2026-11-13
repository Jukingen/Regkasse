using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs
{
    public class InvoiceListItemDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string? CustomerName { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public InvoiceStatus Status { get; set; }
        /// <summary>Authoritative register row id (FK). Use for admin links/filters; not the same as <see cref="KassenId"/> display text.</summary>
        public Guid CashRegisterId { get; set; }
        public string KassenId { get; set; } = string.Empty;
        // TSE Signature can be large and might not be needed for list view, but requested as optional in prompt.
        // Keeping it out for performance unless specifically asked, but prompt said "tseSignature? (opsiyonel)"
        // so I will include it as nullable.
        public string? TseSignature { get; set; }
        public DocumentType DocumentType { get; set; }
        public Guid? OriginalInvoiceId { get; set; }

        /// <summary>
        /// List row semantics: <c>PersistedInvoice</c> = row from <c>invoices</c> table; <c>PaymentDerivedListRow</c> = row built from <c>PaymentDetails</c> (POS list) — not a substitute for persisted invoice unless backfilled.
        /// </summary>
        public string ListRowOrigin { get; set; } = "PersistedInvoice";
    }
}
