using System;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// List item DTO for admin receipts list (paginated).
    /// </summary>
    public class ReceiptListItemDto
    {
        public Guid ReceiptId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public string? CashierId { get; set; }
        public string CashRegisterId { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
