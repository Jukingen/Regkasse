using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("customers")]
    public class Customer : BaseEntity, ITenantEntity
    {
        /// <summary>Owning tenant. System customers (e.g. walk-in guest) are seeded to the default tenant but remain visible cross-tenant via the query filter's IsSystem exemption.</summary>
        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("customer_number")]
        [MaxLength(20)]
        public string CustomerNumber { get; set; } = string.Empty;

        [Column("email")]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Column("phone")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Column("address")]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [Column("tax_number")]
        [MaxLength(20)]
        public string TaxNumber { get; set; } = string.Empty;

        [Column("customer_category")]
        public CustomerCategory Category { get; set; } = CustomerCategory.Regular;

        [Column("loyalty_points")]
        public int LoyaltyPoints { get; set; } = 0;

        [Column("total_spent")]
        public decimal TotalSpent { get; set; } = 0;

        [Column("visit_count")]
        public int VisitCount { get; set; } = 0;

        [Column("last_visit")]
        public DateTime? LastVisit { get; set; }

        [Column("notes")]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Optional clinical record for vet and doctor profiles. Column <c>clinical_data</c>
        /// keeps legacy pet keys and the newer clinical keys in the same JSON object.
        /// </summary>
        [Column("clinical_data", TypeName = "jsonb")]
        public CustomerPetData? PetData { get; set; }

        /// <summary>
        /// Optional structured service address. Stored as JSONB so other profiles
        /// keep the existing flat <see cref="Address"/> column.
        /// </summary>
        [Column("address_data", TypeName = "jsonb")]
        public CustomerAddressData? AddressData { get; set; }

        [Column("is_vip")]
        public bool IsVip { get; set; } = false;

        /// <summary>System-managed customer (e.g. walk-in guest). Non–Super Admin users cannot modify or delete.</summary>
        [Column("is_system")]
        public bool IsSystem { get; set; } = false;

        [Column("discount_percentage")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Column("birth_date")]
        public DateTime? BirthDate { get; set; }

        [Column("preferred_payment_method")]
        public CustomerPaymentMethod PreferredPaymentMethod { get; set; } = CustomerPaymentMethod.Cash;

        /// <summary>Optional FK to AspNetUsers. When set, this customer is the benefit identity for that employee (POS staff benefits).</summary>
        [Column("application_user_id")]
        [MaxLength(450)]
        public string? ApplicationUserId { get; set; }

        public virtual ApplicationUser? ApplicationUser { get; set; }

        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public virtual ICollection<BenefitAssignment> BenefitAssignments { get; set; } = new List<BenefitAssignment>();
    }

    public sealed class CustomerPetData
    {
        [MaxLength(100)]
        public string? PetName { get; set; }

        [MaxLength(100)]
        public string? PetSpecies { get; set; }

        [MaxLength(100)]
        public string? PetBreed { get; set; }

        public DateOnly? PetBirthDate { get; set; }

        /// <summary>Vet species. Mirrors <see cref="PetSpecies"/> so older rows stay readable.</summary>
        [MaxLength(100)]
        public string? Species { get; set; }

        /// <summary>Vet breed. Mirrors <see cref="PetBreed"/>.</summary>
        [MaxLength(100)]
        public string? Breed { get; set; }

        /// <summary>Shared birth date. Mirrors <see cref="PetBirthDate"/> for existing vet rows.</summary>
        public DateOnly? BirthDate { get; set; }

        [MaxLength(500)]
        public string? Vaccines { get; set; }

        [MaxLength(500)]
        public string? Allergies { get; set; }

        [MaxLength(500)]
        public string? ChronicConditions { get; set; }

        [MaxLength(200)]
        public string? Insurance { get; set; }

        /// <summary>
        /// Copies legacy pet keys and the generalized keys onto each other.
        /// Existing JSON that only has <c>PetSpecies</c> still loads as <see cref="Species"/>.
        /// </summary>
        public void AlignLegacyKeys()
        {
            Species ??= NullIfBlank(PetSpecies);
            PetSpecies ??= NullIfBlank(Species);
            Breed ??= NullIfBlank(PetBreed);
            PetBreed ??= NullIfBlank(Breed);
            BirthDate ??= PetBirthDate;
            PetBirthDate ??= BirthDate;
            PetSpecies = NullIfBlank(PetSpecies);
            Species = NullIfBlank(Species);
            PetBreed = NullIfBlank(PetBreed);
            Breed = NullIfBlank(Breed);
            PetName = NullIfBlank(PetName);
            Vaccines = NullIfBlank(Vaccines);
            Allergies = NullIfBlank(Allergies);
            ChronicConditions = NullIfBlank(ChronicConditions);
            Insurance = NullIfBlank(Insurance);
        }

        public bool HasContent() =>
            !string.IsNullOrWhiteSpace(PetName)
            || !string.IsNullOrWhiteSpace(PetSpecies)
            || !string.IsNullOrWhiteSpace(Species)
            || !string.IsNullOrWhiteSpace(PetBreed)
            || !string.IsNullOrWhiteSpace(Breed)
            || PetBirthDate is not null
            || BirthDate is not null
            || !string.IsNullOrWhiteSpace(Vaccines)
            || !string.IsNullOrWhiteSpace(Allergies)
            || !string.IsNullOrWhiteSpace(ChronicConditions)
            || !string.IsNullOrWhiteSpace(Insurance);

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>JSONB (de)serializer for <see cref="Customer.PetData"/> / <c>clinical_data</c>.</summary>
    public static class CustomerClinicalDataJson
    {
        public static string? Serialize(CustomerPetData? value)
        {
            if (value is null)
                return null;
            value.AlignLegacyKeys();
            return System.Text.Json.JsonSerializer.Serialize(value);
        }

        public static CustomerPetData? Deserialize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var data = System.Text.Json.JsonSerializer.Deserialize<CustomerPetData>(value);
            data?.AlignLegacyKeys();
            return data;
        }
    }

    public sealed class CustomerAddressData
    {
        [MaxLength(200)]
        public string? Street { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }
    }

    public enum CustomerCategory
    {
        Regular = 0,
        VIP = 1,
        Premium = 2,
        Corporate = 3,
        Student = 4,
        Senior = 5
    }

    public enum CustomerPaymentMethod
    {
        Cash = 0,
        Card = 1,
        Voucher = 2,
        Mobile = 3
    }
}
