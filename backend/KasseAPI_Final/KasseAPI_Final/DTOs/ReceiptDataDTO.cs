using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Receipt data response DTO
    /// </summary>
    public class ReceiptDataDTO
    {
        public string ReceiptNumber { get; set; } = string.Empty;
        public Guid PaymentId { get; set; }
        public DateTime Timestamp { get; set; }
        public int? TableNumber { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public List<ReceiptItemDTO> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TseSignature { get; set; }
        public CompanyInfoDTO CompanyInfo { get; set; } = new();
    }

    public class ReceiptItemDTO
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class CompanyInfoDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
    }
}
