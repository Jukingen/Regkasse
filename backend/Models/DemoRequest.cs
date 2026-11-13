using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Models;

/// <summary>
/// Anonymous marketing-site demo request. Not tenant-scoped: the mandant does not exist yet.
/// </summary>
[Table("demo_requests")]
[Index(nameof(CreatedAtUtc), Name = "ix_demo_requests_created_at_utc")]
public sealed class DemoRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("company")]
    public string? Company { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("industry")]
    public string Industry { get; set; } = string.Empty;

    [Column("message")]
    public string? Message { get; set; }

    /// <summary><see cref="DemoRequestStatuses"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = DemoRequestStatuses.New;
}

public static class DemoRequestStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Closed = "Closed";
}
