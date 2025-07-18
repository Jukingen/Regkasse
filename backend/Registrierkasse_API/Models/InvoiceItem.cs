using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class InvoiceItem : BaseEntity
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public TaxType TaxType { get; set; } = TaxType.Standard;
        public Guid InvoiceId { get; set; }
        public string Product { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public virtual Invoice Invoice { get; set; } = null!;
    }
} 
