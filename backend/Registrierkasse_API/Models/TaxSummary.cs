using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class TaxSummary : BaseEntity
    {
        public decimal StandardTaxBase { get; set; }
        public decimal StandardTaxAmount { get; set; }
        public decimal ReducedTaxBase { get; set; }
        public decimal ReducedTaxAmount { get; set; }
        public decimal SpecialTaxBase { get; set; }
        public decimal SpecialTaxAmount { get; set; }
        public decimal ZeroTaxBase { get; set; }
        public decimal ExemptTaxBase { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        
        // Eski property'ler
        public decimal Standard { get; set; }
        public decimal Reduced { get; set; }
        public decimal Special { get; set; }
    }
} 