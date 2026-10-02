using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries;

/// <inheritdoc cref="ICountryStrategyContext" />
public sealed class CountryStrategyContext : ICountryStrategyContext
{
    private readonly AppDbContext _db;
    private readonly ICountryProfileRegistry _registry;
    private readonly ISettingsTenantResolver? _tenantResolver;
    private readonly IEn16931XmlBuilder _en16931;

    public CountryStrategyContext(
        AppDbContext db,
        ICountryProfileRegistry registry,
        ISettingsTenantResolver? tenantResolver = null,
        IEn16931XmlBuilder? en16931 = null)
    {
        _db = db;
        _registry = registry;
        _tenantResolver = tenantResolver;
        _en16931 = en16931 ?? new En16931UblXmlBuilder();
    }

    public IEn16931XmlBuilder SelectEn16931Builder(CountryStrategyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!string.Equals(binding.Profile.Code, CountryProfileCodes.EuDefault, StringComparison.OrdinalIgnoreCase))
            throw new EInvoicingNotSupportedForCountryException("EN 16931 UBL", binding.Profile.Code);

        return _en16931;
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
