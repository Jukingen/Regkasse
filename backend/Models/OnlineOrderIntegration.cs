using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// One inbound marketplace order per tenant and provider.
/// Not a receipt. <see cref="RawPayload"/> is the only stored body; do not log it.
/// </summary>
[Table("online_order_integrations")]
public class OnlineOrderIntegration : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("external_order_id")]
    public string ExternalOrderId { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("external_channel")]
    public string? ExternalChannel { get; set; }

    [MaxLength(128)]
    [Column("external_location_id")]
    public string? ExternalLocationId { get; set; }

    [Column("online_order_id")]
    public Guid? OnlineOrderId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = OnlineOrderIntegrationStatuses.Received;

    /// <summary>Original JSON. Approved storage. Never write this to logs.</summary>
    [Column("raw_payload")]
    public string? RawPayload { get; set; }

    [MaxLength(64)]
    [Column("payload_sha256", TypeName = "char(64)")]
    public string? PayloadSha256 { get; set; }

    [MaxLength(500)]
    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("next_attempt_at")]
    public DateTime? NextAttemptAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Maps a provider location to one tenant. The webhook body is not the tenant source.
/// </summary>
[Table("online_order_integration_locations")]
public class OnlineOrderIntegrationLocation : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("external_location_id")]
    public string ExternalLocationId { get; set; } = string.Empty;

    /// <summary>Cart owner. Not required to hold <c>digital.orders.approve</c>.</summary>
    [Column("claiming_user_id")]
    public string? ClaimingUserId { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Column("delivery_fee_product_id")]
    public Guid? DeliveryFeeProductId { get; set; }

    [Column("service_fee_product_id")]
    public Guid? ServiceFeeProductId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Maps one provider product id to one catalog product for a tenant.
/// Inactive rows are ignored by marketplace intake.
/// </summary>
[Table("online_order_product_mappings")]
public class OnlineOrderProductMapping : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("external_product_id")]
    public string ExternalProductId { get; set; } = string.Empty;

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

public static class OnlineOrderIntegrationStatuses
{
    public const string Received = "received";
    public const string Mapped = "mapped";
    public const string CartLinked = "cart_linked";
    public const string Failed = "failed";
    public const string Rejected = "rejected";
    public const string Duplicate = "duplicate";

    public const int MaxAttempts = 6;
}

/// <summary>
/// Per-provider signature confirmation. Config cannot set these constants to true.
/// Production location enable stays blocked for a provider while its constant is false.
/// </summary>
public static class OnlineOrderIntegrationSignatureStatus
{
    public const bool DeliverectConfirmed = false;

    public const bool WoltConfirmed = false;

    /// <summary>
    /// Test assembly only (<c>InternalsVisibleTo</c>). Production code never sets this.
    /// Callers reset it to <c>false</c> in a <c>finally</c> block.
    /// </summary>
    internal static bool WoltConfirmedForTests { get; set; }

    public const string HeaderName = "X-Regkasse-Integration-Signature";

    /// <summary>Wolt marketplace webhook header. HMAC-SHA256 hex of the raw body.</summary>
    public const string WoltHeaderName = "WOLT-SIGNATURE";

    public const string WoltProvider = "wolt";

    public const string UnconfirmedCode = "INTEGRATION_SIGNATURE_UNCONFIRMED";

    /// <summary>Pending Deliverect confirmation. Not Deliverect's published header.</summary>
    public const string PendingConfirmationNote = "pending Deliverect confirmation";

    public static bool IsWolt(string? provider) =>
        string.Equals((provider ?? string.Empty).Trim(), WoltProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Production gate. Deliverect reads <see cref="DeliverectConfirmed"/> only.
    /// Wolt reads <see cref="WoltConfirmed"/> or the test hook. Every other provider stays closed.
    /// </summary>
    public static bool IsConfirmed(string? provider)
    {
        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == WoltProvider)
            return WoltConfirmed || WoltConfirmedForTests;
        if (normalized == "deliverect")
            return DeliverectConfirmed;
        return false;
    }
}
