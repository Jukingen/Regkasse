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
        public string KassenId { get; set; } = string.Empty;
        // TSE Signature can be large and might not be needed for list view, but requested as optional in prompt.
        // Keeping it out for performance unless specifically asked, but prompt said "tseSignature? (opsiyonel)"
        // so I will include it as nullable.
        public string? TseSignature { get; set; }
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
