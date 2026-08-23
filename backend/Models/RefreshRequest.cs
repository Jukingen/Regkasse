namespace KasseAPI_Final.Models;

/// <summary>
/// Body for <c>POST /api/Auth/refresh</c>.
/// Optional <see cref="TenantId"/> re-binds the session JWT <c>tenant_id</c> (dev tenant switcher / Super Admin).
/// Browser FA may omit <see cref="RefreshToken"/> and send the HttpOnly admin refresh cookie instead.
/// Optional <see cref="ClientApp"/> selects <c>rk_admin_*</c> vs <c>rk_pos_*</c> when both cookies are present.
/// </summary>
public sealed class RefreshRequest
{
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When set, refresh rotation updates <c>auth_sessions.tenant_id</c> and issues a JWT for this tenant.
    /// Allowed for SuperAdmin (any active tenant) or users with an active membership in the tenant.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// <c>admin</c> or <c>pos</c>. Used to pick the HttpOnly refresh cookie when the body token is omitted.
    /// Also accepted via <c>X-App-Context</c>.
    /// </summary>
    public string? ClientApp { get; set; }
}
