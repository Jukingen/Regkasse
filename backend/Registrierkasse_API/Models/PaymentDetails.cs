using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class PaymentDetails : BaseEntity
    {
        public PaymentMethod PaymentMethod { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public string CardType { get; set; } = string.Empty;
        public string CardLastDigits { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public decimal ChangeAmount { get; set; }
        
        // Eski property'ler
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
} 