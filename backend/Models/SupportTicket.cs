using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Mandanten-Admin support ticket (own tenant). Super Admin reviews all.</summary>
[Table("support_tickets")]
public class SupportTicket : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("ticket_number")]
    public string TicketNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column("category")]
    public string Category { get; set; } = SupportTicketCategories.Technical;

    [Required]
    [MaxLength(16)]
    [Column("priority")]
    public string Priority { get; set; } = SupportTicketPriorities.Medium;

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = SupportTicketStatuses.Open;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Opening message body (also stored as the first <see cref="SupportTicketMessage"/>).</summary>
    [Required]
    [MaxLength(4000)]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("created_by_display_name")]
    public string? CreatedByDisplayName { get; set; }

    /// <summary>Identity user id of the assigned Super Admin (string PK, not Guid).</summary>
    [MaxLength(450)]
    [Column("assigned_to_user_id")]
    public string? AssignedToUserId { get; set; }

    [MaxLength(200)]
    [Column("assigned_to_display_name")]
    public string? AssignedToDisplayName { get; set; }

    [Column("resolved_at_utc")]
    public DateTime? ResolvedAtUtc { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
}

[Table("support_ticket_messages")]
public class SupportTicketMessage : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("ticket_id")]
    public Guid TicketId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public virtual SupportTicket? Ticket { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("author_user_id")]
    public string AuthorUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("author_display_name")]
    public string? AuthorDisplayName { get; set; }

    [Required]
    [MaxLength(4000)]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("is_staff_reply")]
    public bool IsStaffReply { get; set; }

    /// <summary>Internal staff note — hidden from Mandanten-Admin.</summary>
    [Column("is_internal")]
    public bool IsInternal { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class SupportTicketCategories
{
    public const string Technical = "Technical";
    public const string Billing = "Billing";
    public const string License = "License";
    public const string FeatureRequest = "FeatureRequest";
    public const string General = "General";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Technical,
        Billing,
        License,
        FeatureRequest,
        General,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class SupportTicketPriorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Urgent = "Urgent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Low,
        Medium,
        High,
        Urgent,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class SupportTicketStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string WaitingOnTenant = "WaitingOnTenant";
    public const string WaitingOnStaff = "WaitingOnStaff";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Open,
        InProgress,
        WaitingOnTenant,
        WaitingOnStaff,
        Resolved,
        Closed,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());

    public static bool IsOpenLike(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        return !status.Equals(Resolved, StringComparison.OrdinalIgnoreCase)
            && !status.Equals(Closed, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTerminal(string? status) =>
        status is not null
        && (status.Equals(Resolved, StringComparison.OrdinalIgnoreCase)
            || status.Equals(Closed, StringComparison.OrdinalIgnoreCase));
}
