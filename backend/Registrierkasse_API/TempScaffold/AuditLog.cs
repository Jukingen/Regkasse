using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class AuditLog
{
    public Guid Id { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? UserName { get; set; }

    public string? UserRole { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public string? ErrorMessage { get; set; }

    public string? AdditionalData { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
