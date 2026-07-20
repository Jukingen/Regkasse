using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models;

/// <summary>
/// Body for <c>POST /api/Auth/refresh</c>.
/// Optional <see cref="TenantId"/> re-binds the session JWT <c>tenant_id</c> (dev tenant switcher / Super Admin).
/// </summary>
public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When set, refresh rotation updates <c>auth_sessions.tenant_id</c> and issues a JWT for this tenant.
    /// Allowed for SuperAdmin (any active tenant) or users with an active membership in the tenant.
    /// </summary>
    public Guid? TenantId { get; set; }
}
