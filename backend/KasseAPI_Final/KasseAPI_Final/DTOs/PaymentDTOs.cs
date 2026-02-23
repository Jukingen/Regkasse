using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Yeni ödeme oluşturma request'i
    /// </summary>
    public class CreatePaymentRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        public List<PaymentItemRequest> Items { get; set; } = new();
        
        [Required]
        public PaymentMethodRequest Payment { get; set; } = new();
        
        // Yeni eklenen alanlar
        [Required]
        public int TableNumber { get; set; } // Masa numarası
        
        [Required]
        public string CashierId { get; set; } = string.Empty; // Kasiyer ID
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0")]
        public decimal TotalAmount { get; set; } // Toplam tutar
        
        // Avusturya yasal gereksinimleri
        [Required]
        [RegularExpression(@"^ATU\d{8}$", ErrorMessage = "Steuernummer must be in format ATU12345678")]
        public string Steuernummer { get; set; } = string.Empty; // Vergi numarası (ATU12345678)
        
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "KassenId must be between 3 and 50 characters")]
        public string KassenId { get; set; } = string.Empty; // Kasa ID
        
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Ödeme kalemi request'i
    /// </summary>
    public class PaymentItemRequest
    {
        [Required]
        public Guid ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int Quantity { get; set; }
        
        [Required]
        public int TaxType { get; set; } = 1; // 1: standard, 2: reduced, 3: special
    }
    
    /// <summary>
    /// Ödeme yöntemi request'i
    /// </summary>
    public class PaymentMethodRequest
    {
        [Required]
        public string Method { get; set; } = "cash"; // cash, card, voucher
        
        [Required]
        public bool TseRequired { get; set; } = true;
        
        public decimal? Amount { get; set; }
    }
    
    /// <summary>
    /// Ödeme sonucu
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PaymentDetails? Payment { get; set; }
        public Guid? PaymentId { get; set; }
        public string? TseSignature { get; set; }
        public List<string> Errors { get; set; } = new();
    }
    
    /// <summary>
    /// Ödeme istatistikleri
    /// </summary>
    public class PaymentStatistics
    {
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, int> PaymentsByMethod { get; set; } = new();
        public Dictionary<string, decimal> AmountByMethod { get; set; } = new();
        public Dictionary<string, int> PaymentsByTaxType { get; set; } = new();
        
        // TSE ve FinanzOnline istatistikleri
        public int TseSignedPayments { get; set; }
        public decimal TseSignedAmount { get; set; }
        public int FinanzOnlineSentPayments { get; set; }
        public decimal FinanzOnlineSentAmount { get; set; }
    }
}
