namespace KasseAPI_Final.Models.DTOs;

/// <summary>Safe user projection for GET /api/admin/users.</summary>
public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
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
    /// <summary>Concurrency stamp for If-Match (optimistic concurrency).</summary>
    public string? Etag { get; set; }
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    /// <summary><c>Platform</c> or <c>Tenant</c>.</summary>
    public string? UserType { get; set; }
}
