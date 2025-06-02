using System.ComponentModel.DataAnnotations;

namespace Registrierkasse.Models
{
    public enum PaymentMethod
    {
        [Display(Name = "Bargeld")]
        Cash,
        
        [Display(Name = "Kreditkarte")]
        Card,
        
        [Display(Name = "Gutschein")]
        Voucher,
        
        [Display(Name = "Überweisung")]
        Transfer,
        
        [Display(Name = "PayPal")]
        PayPal
    }
} 