using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.AdminTenants;

public interface ITenantProvisioningService
{
    /// <summary>
    /// Seeds default cash register, admin user, category, and demo products for a new tenant.
    /// Caller must have persisted <paramref name="tenant"/> and should run inside a transaction when possible.
    /// </summary>
    Task<(TenantProvisioningResult? Result, string? Error)> ProvisionAsync(
        Tenant tenant,
        string? adminEmail,
        string? adminPassword,
        bool grantTrialLicense,
        bool importDemoMenu = false,
        string? cashRegisterNumber = null,
        CancellationToken cancellationToken = default);
}
