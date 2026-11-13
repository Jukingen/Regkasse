using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-owned marketplace credentials. Ciphertext only. Not used for Wolt.
/// </summary>
[Table("online_order_integration_tenant_credentials")]
public class OnlineOrderIntegrationTenantCredential : ITenantEntity
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

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("chain_id")]
    public string ChainId { get; set; } = string.Empty;

    /// <summary>Data Protection ciphertext. Vendor Portal webhook secret. Never log.</summary>
    [Required]
    [Column("webhook_token")]
    public string WebhookToken { get; set; } = string.Empty;

    /// <summary>Cached access token ciphertext. Null until the first client-credentials grant.</summary>
    [Column("access_token")]
    public string? AccessToken { get; set; }

    [Column("access_token_expires_at")]
    public DateTime? AccessTokenExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
