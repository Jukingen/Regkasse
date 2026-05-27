using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>POST /api/admin/users — platform or tenant user (no invitation email).</summary>
public sealed class AdminCreateUserRequest
{
    /// <summary>When set, creates a tenant-scoped user under this mandant.</summary>
    public Guid? TenantId { get; set; }

    public bool IsOwner { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Optional operator-provided password; generated securely when omitted.</summary>
    [MinLength(8)]
    [MaxLength(128)]
    public string? Password { get; set; }

    /// <summary>Optional login name; generated from role when omitted (e.g. admin1, manager2).</summary>
    [MaxLength(256)]
    public string? UserName { get; set; }

    [MaxLength(20)]
    public string? EmployeeNumber { get; set; }

    [MaxLength(20)]
    public string? TaxNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>Created user plus one-time password for operator handoff (never emailed).</summary>
public sealed class AdminCreateUserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? Role { get; set; }
    public string? TaxNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public string? Etag { get; set; }
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public string? UserType { get; set; }
    public string GeneratedPassword { get; set; } = string.Empty;
}
