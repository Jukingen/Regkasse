using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Recurring digital-service subscription (website / app add-on). Not a Mandanten <see cref="LicenseSale"/>.
/// </summary>
[Table("digital_service_subscriptions")]
public class Subscription : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Catalog key from <see cref="ServicePricingData"/> (e.g. website-starter).</summary>
    [Required]
    [MaxLength(64)]
    [Column("service_id")]
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>Monthly list price snapshotted at create time (EUR).</summary>
    [Column("price", TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "EUR";

    /// <summary><see cref="SubscriptionStatuses"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = SubscriptionStatuses.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("next_billing_date")]
    public DateTime NextBillingDate { get; set; }

    [Column("cancelled_at_utc")]
    public DateTime? CancelledAtUtc { get; set; }

    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    [MaxLength(450)]
    [Column("cancelled_by_user_id")]
    public string? CancelledByUserId { get; set; }
}

/// <summary>Allowed <see cref="Subscription.Status"/> values.</summary>
public static class SubscriptionStatuses
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";
    public const string PastDue = "past_due";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Active,
        Cancelled,
        PastDue,
    };
}
