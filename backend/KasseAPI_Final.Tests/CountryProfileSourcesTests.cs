using System.Text.RegularExpressions;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 13: pin every CountryProfile seed field and document the missing
/// <c>// Source:</c> comments as an explicit known gap (Paket 13-b).
/// Does not change production seeds. VAT rates are not on the profile (Paket 13-c).
/// </summary>
public sealed class CountryProfileSourcesTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    private static readonly Regex SourceCommentPattern =
        new(@"^\s*//\s*Source:", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// Known gap: CountryProfileRegistry seeds have no <c>// Source:</c> comments yet.
    /// Expected count is 0 until Paket 13-b adds them; that change must update this assertion.
    /// </summary>
    [Fact]
    public void SourceComments_AreADocumentedKnownGap()
    {
        var registryPath = Path.Combine(FindBackendRoot(), "Services", "Countries", "CountryProfileRegistry.cs");
        Assert.True(File.Exists(registryPath), $"CountryProfileRegistry.cs not found at '{registryPath}'.");

        var sourceCount = CountSourceComments(registryPath);
        Assert.True(
            sourceCount == 0,
            "PAKET 13-B: add // Source: comments to CountryProfile seeds. See docs/COUNTRIES.md Seed Sources.");
    }

    [Fact]
    public void AustriaSeed_PinsCurrentProductionMirroringValues()
    {
        var austria = Registry.Get(CountryProfileCodes.Austria);

        Assert.Equal(CountryProfileCodes.Austria, austria.Code);
        Assert.Equal("Austria", austria.Name);
        Assert.True(austria.IsTenantSelectable);
        Assert.Equal("EUR", austria.Currency);
        // DRIFT: BCP-47 AT = de-AT. See Paket 13-b.
        Assert.Equal("de-DE", austria.DefaultLocale);
        Assert.Equal("Europe/Vienna", austria.DefaultTimeZone);
        Assert.Equal(FiscalSystem.RKSV_AT, austria.FiscalSystem);
        Assert.Empty(austria.EInvoicingStandards);
        Assert.Equal(@"^ATU\d{8}$", austria.VatIdPattern);
        Assert.Equal(VatIdPatterns.Austria, austria.VatIdPattern);
        Assert.Equal(
            [
                VatRegime.AT_RKSV_STANDARD,
                VatRegime.EU_REVERSE_CHARGE,
                VatRegime.EU_OSS,
                VatRegime.NON_EU,
            ],
            austria.AllowedVatRegimes.ToArray());
    }

    [Fact]
    public void GermanySeed_PinsCurrentShape()
    {
        var germany = Registry.Get(CountryProfileCodes.Germany);

        Assert.Equal(CountryProfileCodes.Germany, germany.Code);
        Assert.Equal("Germany", germany.Name);
        Assert.True(germany.IsTenantSelectable);
        Assert.Equal("EUR", germany.Currency);
        Assert.Equal("de-DE", germany.DefaultLocale);
        Assert.Equal("Europe/Berlin", germany.DefaultTimeZone);
        Assert.Equal(FiscalSystem.KASSENSICHERHEIT_DE, germany.FiscalSystem);
        Assert.Equal(
            [EInvoicingStandard.ZUGFERD, EInvoicingStandard.XRECHNUNG],
            germany.EInvoicingStandards.ToArray());
        Assert.Equal(@"^DE\d{9}$", germany.VatIdPattern);
        Assert.Equal(VatIdPatterns.Germany, germany.VatIdPattern);
        Assert.Equal(
            [
                VatRegime.DE_USTG_STANDARD,
                VatRegime.DE_KLEINUNTERNEHMER,
                VatRegime.EU_REVERSE_CHARGE,
                VatRegime.EU_OSS,
                VatRegime.NON_EU,
            ],
            germany.AllowedVatRegimes.ToArray());
    }

    [Fact]
    public void SwitzerlandSeed_PinsCurrentShape()
    {
        var switzerland = Registry.Get(CountryProfileCodes.Switzerland);

        Assert.Equal(CountryProfileCodes.Switzerland, switzerland.Code);
        Assert.Equal("Switzerland", switzerland.Name);
        Assert.True(switzerland.IsTenantSelectable);
        Assert.Equal("CHF", switzerland.Currency);
        Assert.Equal("de-CH", switzerland.DefaultLocale);
        Assert.Equal("Europe/Zurich", switzerland.DefaultTimeZone);
        Assert.Equal(FiscalSystem.MWST_CH, switzerland.FiscalSystem);
        Assert.Equal(
            [EInvoicingStandard.QR_RECHNUNG],
            switzerland.EInvoicingStandards.ToArray());
        Assert.Equal(@"^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$", switzerland.VatIdPattern);
        Assert.Equal(VatIdPatterns.Switzerland, switzerland.VatIdPattern);
        Assert.Equal(
            [
                VatRegime.CH_MWST_STANDARD,
                VatRegime.CH_KLEINUNTERNEHMER,
                VatRegime.NON_EU,
            ],
            switzerland.AllowedVatRegimes.ToArray());
    }

    [Fact]
    public void EuDefaultSeed_PinsRegistryOnlyFallback()
    {
        var euDefault = Registry.Get(CountryProfileCodes.EuDefault);

        Assert.Equal(CountryProfileCodes.EuDefault, euDefault.Code);
        Assert.Equal("European Union (default profile)", euDefault.Name);
        Assert.False(euDefault.IsTenantSelectable);
        Assert.Equal("EUR", euDefault.Currency);
        Assert.Equal("en", euDefault.DefaultLocale);
        Assert.Equal("UTC", euDefault.DefaultTimeZone);
        Assert.Equal(FiscalSystem.NONE, euDefault.FiscalSystem);
        Assert.Equal(
            [EInvoicingStandard.EN_16931],
            euDefault.EInvoicingStandards.ToArray());
        Assert.Equal(@"^[A-Z]{2}[A-Za-z0-9+*.]{2,12}$", euDefault.VatIdPattern);
        Assert.Equal(VatIdPatterns.EuDefault, euDefault.VatIdPattern);
        Assert.Equal(
            [
                VatRegime.EU_REVERSE_CHARGE,
                VatRegime.EU_OSS,
                VatRegime.NON_EU,
            ],
            euDefault.AllowedVatRegimes.ToArray());
    }

    private static int CountSourceComments(string registryPath) =>
        SourceCommentPattern.Matches(File.ReadAllText(registryPath)).Count;

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
