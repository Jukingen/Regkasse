using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.AdminTenants;

public interface ITenantProvisioningService
{
    /// <summary>
    /// Seeds default cash register, admin user, category, and demo products for a new tenant.
    /// Caller must have persisted <paramref name="tenant"/> and should run inside a transaction when possible.
    /// When <paramref name="countryProfile"/> is omitted, Austria is used (AT regression path).
    /// </summary>
    Task<(TenantProvisioningResult? Result, string? Error)> ProvisionAsync(
        Tenant tenant,
        string? adminEmail,
        string? adminPassword,
        bool grantTrialLicense,
        bool importDemoMenu = false,
        string? cashRegisterNumber = null,
        bool seedIndustryStarterUsers = true,
        int? trialDurationDays = null,
        CountryProfile? countryProfile = null,
        VatRegime vatRegime = VatRegime.AT_RKSV_STANDARD,
        CancellationToken cancellationToken = default);
}
