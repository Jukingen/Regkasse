using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/admin/tenants/{tenantId}/users — manual tenant user create.</summary>
public sealed class CreateTenantUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional login name; generated from role when omitted (e.g. manager1).</summary>
    [MaxLength(256)]
    public string? UserName { get; set; }

    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
}
