using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("product_imeis")]
public sealed class ProductImei : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("imei")]
    public string Imei { get; set; } = string.Empty;

    [Column("status")]
    public ProductImeiStatus Status { get; set; } = ProductImeiStatus.InStock;

    [Column("sold_payment_id")]
    public Guid? SoldPaymentId { get; set; }

    public PaymentDetails? SoldPayment { get; set; }

    [Column("warranty_months")]
    public int WarrantyMonths { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("sold_at_utc")]
    public DateTime? SoldAtUtc { get; set; }
}
