using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("refresh_tokens")]
public sealed class RefreshToken
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Required]
    [Column("token_hash")]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [Column("access_jti")]
    [MaxLength(64)]
    public string AccessJti { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("expires_at_utc")]
    public DateTime ExpiresAtUtc { get; set; }

    [Column("consumed_at_utc")]
    public DateTime? ConsumedAtUtc { get; set; }

    [Column("revoked_at_utc")]
    public DateTime? RevokedAtUtc { get; set; }

    [Column("replaced_by_token_id")]
    public Guid? ReplacedByTokenId { get; set; }

    [Column("revoke_reason")]
    [MaxLength(200)]
    public string? RevokeReason { get; set; }

    public AuthSession? Session { get; set; }
}
