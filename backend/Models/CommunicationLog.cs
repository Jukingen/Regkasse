using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Super Admin outbound communication log (non-fiscal).</summary>
[Table("communication_log")]
public class CommunicationLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(320)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>sent | failed</summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = CommunicationLogStatuses.Sent;

    [MaxLength(2000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class CommunicationLogStatuses
{
    public const string Sent = "sent";
    public const string Failed = "failed";
}
