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

    [Column("revoked_at_utc")]
    public DateTime? RevokedAtUtc { get; set; }

    [Column("revoked_reason")]
    [MaxLength(200)]
    public string? RevokedReason { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
