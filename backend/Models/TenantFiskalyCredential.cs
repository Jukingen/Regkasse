using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-owned fiskaly SIGN AT API key, secret, and SCU. Secrets are Data Protection ciphertext.
/// </summary>
[Table("tenant_fiskaly_credentials")]
public class TenantFiskalyCredential : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("api_key_protected")]
    public string ApiKeyProtected { get; set; } = string.Empty;

    /// <summary>Data Protection ciphertext. Never log or return plaintext.</summary>
    [Required]
    [Column("api_secret_protected")]
    public string ApiSecretProtected { get; set; } = string.Empty;

    /// <summary>Last 4 characters of the API key. Safe to show in FA and audit.</summary>
    [Required]
    [MaxLength(8)]
    [Column("api_key_hint")]
    public string ApiKeyHint { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("scu_id")]
    public string ScuId { get; set; } = string.Empty;

    /// <summary><c>TEST</c> or <c>LIVE</c>.</summary>
    [Required]
    [MaxLength(8)]
    [Column("environment")]
    public string Environment { get; set; } = "TEST";

    /// <summary>Public leaf certificate (DER, Base64) for DEP. Fiskaly keeps the private key.</summary>
    [Column("signing_certificate_der_base64")]
    public string? SigningCertificateDerBase64 { get; set; }

    [Column("issuer_certificates_der_base64", TypeName = "jsonb")]
    public List<string> IssuerCertificatesDerBase64 { get; set; } = new();

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("updated_by_user_id")]
    public string? UpdatedByUserId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
