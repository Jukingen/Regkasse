using System.Reflection;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Seed contract for the in-code country registry. The Austrian seed is the sensitive one: it must
/// mirror the values already live in production, otherwise a later call-site migration would silently
/// change AT provisioning.
/// </summary>
public sealed class CountryProfileRegistryTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    [Fact]
    public void Registry_SeedsAustriaGermanySwitzerlandAndEuDefault()
    {
        Assert.Equal(
            ["AT", "DE", "CH", "EU_DEFAULT"],
            Registry.All.Select(p => p.Code).ToArray());
    }

    [Fact]
    public void EuDefault_IsRegistryOnlyAndNotTenantSelectable()
    {
        var euDefault = Registry.Get(CountryProfileCodes.EuDefault);

        Assert.False(euDefault.IsTenantSelectable);
        Assert.DoesNotContain(Registry.TenantSelectable, p => p.Code == CountryProfileCodes.EuDefault);
        Assert.Equal(["AT", "DE", "CH"], Registry.TenantSelectable.Select(p => p.Code).ToArray());
    }

    [Theory]
    [InlineData("AT")]
    [InlineData("at")]
    [InlineData(" De ")]
    [InlineData("ch")]
    [InlineData("eu_default")]
    public void TryGet_IsCaseInsensitiveAndTrims(string code)
    {
        Assert.True(Registry.TryGet(code, out var profile));
        Assert.Equal(code.Trim(), profile.Code, ignoreCase: true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    [InlineData("AUT")]
    public void GetOrDefault_FallsBackToAustriaForUnknownOrLegacyValues(string? code)
    {
        Assert.Equal(CountryProfileCodes.Austria, Registry.GetOrDefault(code).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("AUT")]
    public void Get_ThrowsWithAStableErrorCodeForUnknownInput(string? code)
    {
        var ex = Assert.Throws<UnknownCountryCodeException>(() => Registry.Get(code));

        Assert.Equal("UNKNOWN_COUNTRY_CODE", ex.ErrorCode);
        Assert.Equal(code, ex.CountryCode);
    }

    /// <summary>
    /// Regression: these four values are what <c>CompanySettingsController.CreateSettingsShell</c>
    /// writes today. If the seed drifts, AT provisioning changes the moment a call site starts
    /// reading the profile.
    /// </summary>
    [Fact]
    public void AustriaSeed_MirrorsTheLiveCompanySettingsDefaults()
    {
        var austria = Registry.Default;

        Assert.Equal(CountryProfileCodes.Austria, austria.Code);
        Assert.Equal("EUR", austria.Currency);
        Assert.Equal("de-DE", austria.DefaultLocale);
        Assert.Equal("Europe/Vienna", austria.DefaultTimeZone);
        Assert.Equal(FiscalSystem.RKSV_AT, austria.FiscalSystem);
    }

    /// <summary>Regression: the AT seed must equal the UID pattern already enforced on the fiscal path.</summary>
    [Fact]
    public void AustriaSeed_UsesTheLiveUidPattern()
    {
        Assert.Equal(@"^ATU\d{8}$", Registry.Default.VatIdPattern);
    }

    [Fact]
    public void AustriaIsTheOnlyImplementedFiscalSystem()
    {
        Assert.Equal(FiscalSystem.RKSV_AT, Registry.Get("AT").FiscalSystem);
        Assert.Equal(FiscalSystem.KASSENSICHERHEIT_DE, Registry.Get("DE").FiscalSystem);
        Assert.Equal(FiscalSystem.MWST_CH, Registry.Get("CH").FiscalSystem);
        Assert.Equal(FiscalSystem.NONE, Registry.Get("EU_DEFAULT").FiscalSystem);
    }

    [Theory]
    [InlineData("AT", "ATU12345678", true)]
    [InlineData("AT", "ATU1234567", false)]
    [InlineData("AT", "DE123456789", false)]
    [InlineData("DE", "DE123456789", true)]
    [InlineData("DE", "DE12345678", false)]
    [InlineData("CH", "CHE-123.456.789", true)]
    [InlineData("CH", "CHE-123.456.789 MWST", true)]
    [InlineData("CH", "CHE-123.456.789 TVA", true)]
    [InlineData("CH", "CHE-123.456.78", false)]
    [InlineData("CH", "CHE123456789", false)]
    public void VatIdShape_MatchesThePerCountrySeed(string code, string vatId, bool expected)
    {
        Assert.Equal(expected, Registry.Get(code).MatchesVatIdShape(vatId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VatIdShape_RejectsBlankInput(string? vatId)
    {
        Assert.False(Registry.Default.MatchesVatIdShape(vatId));
    }

    [Fact]
    public void AllowedVatRegimes_AreCountryAppropriate()
    {
        var austria = Registry.Get("AT");
        Assert.True(austria.Supports(VatRegime.AT_RKSV_STANDARD));
        Assert.True(austria.Supports(VatRegime.EU_REVERSE_CHARGE));
        Assert.False(austria.Supports(VatRegime.DE_USTG_STANDARD));

        var germany = Registry.Get("DE");
        Assert.True(germany.Supports(VatRegime.DE_USTG_STANDARD));
        Assert.True(germany.Supports(VatRegime.DE_KLEINUNTERNEHMER));
        Assert.False(germany.Supports(VatRegime.AT_RKSV_STANDARD));

        // Switzerland is outside the EU VAT area.
        var switzerland = Registry.Get("CH");
        Assert.True(switzerland.Supports(VatRegime.CH_MWST_STANDARD));
        Assert.False(switzerland.Supports(VatRegime.EU_REVERSE_CHARGE));
        Assert.False(switzerland.Supports(VatRegime.EU_OSS));
    }

    [Fact]
    public void EveryProfile_HasDistinctNonEmptyAllowedRegimes()
    {
        Assert.All(Registry.All, profile =>
        {
            Assert.NotEmpty(profile.AllowedVatRegimes);
            Assert.Equal(profile.AllowedVatRegimes.Count, profile.AllowedVatRegimes.Distinct().Count());
        });
    }

    [Fact]
    public void EveryProfile_HasCompleteFormattingDefaults()
    {
        Assert.All(Registry.All, profile =>
        {
            Assert.Equal(3, profile.Currency.Length);
            Assert.False(string.IsNullOrWhiteSpace(profile.DefaultLocale));
            Assert.False(string.IsNullOrWhiteSpace(profile.DefaultTimeZone));
            Assert.False(string.IsNullOrWhiteSpace(profile.Name));
            Assert.False(string.IsNullOrWhiteSpace(profile.VatIdPattern));
        });
    }

    /// <summary>
    /// Guard for <c>docs/COUNTRIES.md</c> §13: VAT rates belong with tax types and
    /// <see cref="VatRegime"/>, never on a country profile.
    /// </summary>
    [Fact]
    public void CountryProfile_DoesNotCarryVatRates()
    {
        var rateLikeProperties = typeof(CountryProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Rate", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Percent", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(rateLikeProperties);
    }

    /// <summary>Only tenant-selectable profiles may claim a real ISO 3166-1 alpha-2 code.</summary>
    [Fact]
    public void TenantSelectableProfiles_UseTwoLetterIsoCodes()
    {
        Assert.All(Registry.TenantSelectable, profile =>
            Assert.True(Iso3166CountryCode.IsValid(profile.Code), $"'{profile.Code}' is not alpha-2."));

        Assert.False(Iso3166CountryCode.IsValid(CountryProfileCodes.EuDefault));
    }
}
