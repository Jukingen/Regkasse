using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Deployment-wide marketplace provider switch and partner credentials.
/// Not tenant-scoped. Credential columns hold ciphertext only.
/// </summary>
[Table("online_order_integration_providers")]
public class OnlineOrderIntegrationProvider
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(32)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// <see cref="OnlineOrderIntegrationCredentialOwners.Deployment"/> keeps partner secrets on this row.
    /// <see cref="OnlineOrderIntegrationCredentialOwners.Tenant"/> leaves them on the tenant credential table.
    /// </summary>
    [Required]
    [MaxLength(16)]
    [Column("credential_owner")]
    public string CredentialOwner { get; set; } = OnlineOrderIntegrationCredentialOwners.Deployment;
}

public static class OnlineOrderIntegrationCredentialOwners
{
    public const string Deployment = "deployment";

    public const string Tenant = "tenant";
}
