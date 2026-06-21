namespace KasseAPI_Final.Configuration;

/// <summary>
/// Super-admin permanent tenant deletion policy. Production disables hard-delete by default (compliance).
/// </summary>
public sealed class TenantDeletionOptions
{
    public const string SectionName = "TenantDeletion";

    /// <summary>
    /// When false (default), <c>DELETE /api/admin/tenants/{id}/permanent</c> returns 403 in non-Development environments.
    /// </summary>
    public bool AllowPermanentDeleteInProduction { get; set; }
}
