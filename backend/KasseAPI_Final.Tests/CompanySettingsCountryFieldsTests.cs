using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Guards the additive country/billing columns on <c>company_settings</c> and proves the pre-existing
/// country and VAT-ID surface was reused rather than duplicated (see <c>docs/COUNTRIES.md</c>).
/// </summary>
public sealed class CompanySettingsCountryFieldsTests
{
    [Fact]
    public void BillingCountry_IsNullableTwoCharColumn()
    {
        var property = GetProperty(nameof(CompanySettings.BillingCountry));

        Assert.Equal("billing_country", property.GetColumnName());
        Assert.True(property.IsNullable);
        Assert.Equal(2, property.GetMaxLength());
        Assert.Null(property.GetDefaultValue());
    }

    [Fact]
    public void VatRegime_IsPersistedAsStringWithAustrianDefault()
    {
        var property = GetProperty(nameof(CompanySettings.VatRegime));

        Assert.Equal("vat_regime", property.GetColumnName());
        Assert.False(property.IsNullable);
        Assert.Equal(VatRegimeNames.MaxLength, property.GetMaxLength());
        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, property.GetDefaultValue());
    }

    [Fact]
    public void VatRegime_PersistedNamesMatchTheContractedValues()
    {
        string[] expected =
        [
            "AT_RKSV_STANDARD",
            "DE_USTG_STANDARD",
            "DE_KLEINUNTERNEHMER",
            "CH_MWST_STANDARD",
            "CH_KLEINUNTERNEHMER",
            "EU_REVERSE_CHARGE",
            "EU_OSS",
            "NON_EU",
        ];

        Assert.Equal(expected, Enum.GetNames<VatRegime>());
        Assert.Equal("AT_RKSV_STANDARD", VatRegimeNames.Default);
        Assert.All(expected, name => Assert.True(name.Length <= VatRegimeNames.MaxLength));
    }

    [Fact]
    public void TaxExempt_IsNonNullableBooleanDefaultingToFalse()
    {
        var property = GetProperty(nameof(CompanySettings.TaxExempt));

        Assert.Equal("tax_exempt", property.GetColumnName());
        Assert.False(property.IsNullable);
        Assert.Equal(false, property.GetDefaultValue());
    }

    [Fact]
    public void NewSettingsDefaultToAustrianRegime()
    {
        var settings = new CompanySettings();

        Assert.Equal(VatRegime.AT_RKSV_STANDARD, settings.VatRegime);
        Assert.False(settings.TaxExempt);
        Assert.Null(settings.BillingCountry);
        Assert.Equal("AT", settings.Country);
    }

    /// <summary>Regression: the operating country stayed a single column; no parallel CountryCode was added.</summary>
    [Fact]
    public void Country_WasNotDuplicatedByASecondCountryCodeColumn()
    {
        var entityType = GetEntityType();

        var country = entityType.FindProperty(nameof(CompanySettings.Country));
        Assert.NotNull(country);
        // Case-insensitive: the in-memory provider reports attribute-mapped names in PascalCase.
        // The literal "country" column is pinned by the migration snapshot, not here.
        Assert.Equal("country", country!.GetColumnName(), ignoreCase: true);
        Assert.Equal("AT", country.GetDefaultValue());
        Assert.Equal(2, country.GetMaxLength());

        Assert.Null(entityType.FindProperty("CountryCode"));
        Assert.DoesNotContain(
            entityType.GetProperties(),
            p => string.Equals(p.GetColumnName(), "country_code", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Regression: VAT-ID remains an unmapped alias over CompanyTaxNumber, not a second column.</summary>
    [Fact]
    public void VatId_RemainsAnUnmappedAliasOverCompanyTaxNumber()
    {
        var entityType = GetEntityType();

        Assert.Null(entityType.FindProperty(nameof(CompanySettings.VatId)));
        Assert.DoesNotContain(
            entityType.GetProperties(),
            p => string.Equals(p.GetColumnName(), "vat_id", StringComparison.OrdinalIgnoreCase));

        var taxNumber = entityType.FindProperty(nameof(CompanySettings.CompanyTaxNumber));
        Assert.NotNull(taxNumber);
        Assert.False(taxNumber!.IsNullable);

        var settings = new CompanySettings { CompanyTaxNumber = "ATU12345678" };
        Assert.Equal("ATU12345678", settings.VatId);

        settings.VatId = "ATU87654321";
        Assert.Equal("ATU87654321", settings.CompanyTaxNumber);
    }

    /// <summary>Regression: locale and currency were reused, not shadowed by new preference columns.</summary>
    [Fact]
    public void LocaleAndCurrency_WereNotDuplicated()
    {
        var entityType = GetEntityType();

        Assert.NotNull(entityType.FindProperty(nameof(CompanySettings.Language)));
        Assert.NotNull(entityType.FindProperty(nameof(CompanySettings.Currency)));
        Assert.Null(entityType.FindProperty("PreferredLocale"));
        Assert.Null(entityType.FindProperty("PreferredCurrency"));
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CompanySettingsCountryFields_{Guid.NewGuid():N}")
            .Options;

        using var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        return db.Model.FindEntityType(typeof(CompanySettings))!;
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IProperty GetProperty(string name)
    {
        var property = GetEntityType().FindProperty(name);
        Assert.NotNull(property);
        return property!;
    }
}
