using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// OAuth token pair for one provider location. Columns hold ciphertext only.
/// </summary>
[Table("online_order_integration_oauth_tokens")]
public class OnlineOrderIntegrationOAuthToken : ITenantEntity
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

    /// <summary>Data Protection ciphertext. Never log.</summary>
    [Required]
    [Column("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Data Protection ciphertext. Never log.</summary>
    [Required]
    [Column("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [Column("access_token_expires_at")]
    public DateTime AccessTokenExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
