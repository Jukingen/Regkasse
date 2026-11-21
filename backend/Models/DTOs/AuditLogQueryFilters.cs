namespace KasseAPI_Final.Models.DTOs;

/// <summary>Query filters for audit log list, export, and scheduled reports.</summary>
public sealed class AuditLogQueryFilters
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserId { get; set; }
    public string? UserRole { get; set; }
    /// <summary>User lifecycle target (EntityName or metadata targetUserId).</summary>
    public string? TargetUserId { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public AuditLogStatus? Status { get; set; }
    /// <summary>When <see cref="Status"/> is null: "success" or "failure".</summary>
    public string? StatusOutcome { get; set; }
    public bool? HasChanges { get; set; }

    /// <summary>
    /// When true, omit audit rows whose actor <see cref="AuditLog.UserRole"/> is a platform operator (SuperAdmin).
    /// Applied automatically for non–Super Admin callers on list/export endpoints.
    /// </summary>
    public bool ExcludePlatformOperatorActors { get; set; }

    /// <summary>
    /// Full-text query against the stored search vector (action, entity type, description, actor email).
    /// </summary>
    public string? Search { get; set; }

    /// <summary>When both are set, export returns this page instead of the full filtered set.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }
}
