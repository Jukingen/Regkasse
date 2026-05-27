using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>POST /api/admin/tenants/{tenantId}/users/quick — role only; email/password generated server-side.</summary>
public sealed class CreateQuickTenantUserRequest
{
    [Required]
    [MaxLength(64)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Optional login name; generated from role when omitted (e.g. cashier2).</summary>
    [MaxLength(256)]
    public string? UserName { get; set; }
}
