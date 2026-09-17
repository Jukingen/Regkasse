using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries;

/// <inheritdoc cref="ICountryStrategyContext" />
public sealed class CountryStrategyContext : ICountryStrategyContext
{
    private readonly AppDbContext _db;
    private readonly ICountryProfileRegistry _registry;
    private readonly ISettingsTenantResolver? _tenantResolver;

    public CountryStrategyContext(
        AppDbContext db,
        ICountryProfileRegistry registry,
        ISettingsTenantResolver? tenantResolver = null)
    {
        _db = db;
        _registry = registry;
        _tenantResolver = tenantResolver;
    }

    public async Task<CountryStrategyBinding> LoadAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = Guid.Empty;
        CompanySettings? settings;

        if (_tenantResolver != null)
        {
            tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            settings = await _db.CompanySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            settings = await _db.CompanySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (settings == null)
        {
            var profile = _registry.GetOrDefault(null);
            return new CountryStrategyBinding(
                CreateLegacyAtFallback(tenantId),
                profile,
                VatRegime.AT_RKSV_STANDARD,
                UsedLegacyFallback: true);
        }

        return new CountryStrategyBinding(
            settings,
            _registry.GetOrDefault(settings.Country),
            settings.VatRegime,
            UsedLegacyFallback: false);
    }

    private static CompanySettings CreateLegacyAtFallback(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Country = CountryProfileCodes.Austria,
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        TaxExempt = false,
        CompanyName = string.Empty,
        CompanyAddress = string.Empty,
        CompanyTaxNumber = string.Empty,
    };
}
