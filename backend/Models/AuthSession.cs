using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("auth_sessions")]
public sealed class AuthSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("client_app")]
    [MaxLength(20)]
    public string ClientApp { get; set; } = string.Empty;

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Effective tenant for this session (refresh rotation). Null for legacy rows → default tenant at issuance.</summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("revoked_at_utc")]
    public DateTime? RevokedAtUtc { get; set; }

    [Column("revoked_reason")]
    [MaxLength(200)]
    public string? RevokedReason { get; set; }

    [Column("last_activity_at_utc")]
    public DateTime? LastActivityAtUtc { get; set; }

    [Column("device_id")]
    [MaxLength(200)]
    public string? DeviceId { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
