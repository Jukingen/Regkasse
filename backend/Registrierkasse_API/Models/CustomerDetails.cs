using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class CustomerDetails : BaseEntity
    {
        public string CompanyName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string VatNumber { get; set; } = string.Empty;
        
        // Eski property'ler
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
    }
} 