namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Resolves the current tenant key from the HTTP request (e.g. subdomain slug).
/// </summary>
public interface ITenantProvider
{
    /// <summary>Tenant slug for this request (e.g. <c>companyA</c> from <c>companyA.regkasse.at</c>).</summary>
    string GetCurrentTenantId();
}
