using KasseAPI_Final.Services.Billing;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseKeyGeneratorTests
{
    private readonly LicenseKeyGenerator _generator = new();
    private static readonly DateTime FutureExpiry = new(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void GenerateUnifiedLicenseKey_ProducesExpectedShape()
    {
        var key = _generator.GenerateUnifiedLicenseKey(FutureExpiry, "Cafe");

        Assert.StartsWith("REGK-20991231-cafe-", key, StringComparison.Ordinal);
        Assert.True(_generator.ValidateLicenseKeyFormat(key));
        Assert.True(LicenseKeyGenerator.IsValidRandomPart(key.Split('-')[^1]));
        Assert.Equal(8, key.Split('-')[^1].Length);
    }

    [Fact]
    public void GenerateLicenseKey_DelegatesToUnifiedFormat()
    {
        var key = _generator.GenerateLicenseKey("Cafe", FutureExpiry);

        Assert.StartsWith("REGK-20991231-cafe-", key, StringComparison.Ordinal);
        Assert.True(_generator.ValidateLicenseKeyFormat(key));
    }

    [Fact]
    public void GenerateUnifiedLicenseKey_NormalizesSlug()
    {
        var key = _generator.GenerateUnifiedLicenseKey(new DateTime(2099, 6, 1, 0, 0, 0, DateTimeKind.Utc), "  My_Cafe  ");

        Assert.StartsWith("REGK-20990601-my-cafe-", key, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateUnifiedLicenseKey_ServerLicense_UsesSystemSlug()
    {
        var key = _generator.GenerateUnifiedLicenseKey(FutureExpiry, LicenseKeyGenerator.SystemSlug);

        Assert.StartsWith("REGK-20991231-system-", key, StringComparison.Ordinal);
        Assert.True(LicenseKeyGenerator.IsSystemLicenseKey(key));
        Assert.False(LicenseKeyGenerator.IsMandantBillingKey(key));
        Assert.True(LicenseKeyGenerator.IsDeploymentLicenseKey(key));
    }

    [Fact]
    public void GenerateUnifiedLicenseKey_TenantLicense_UsesTenantSlug()
    {
        var key = _generator.GenerateUnifiedLicenseKey(FutureExpiry, "preview-co");

        Assert.StartsWith("REGK-20991231-preview-co-", key, StringComparison.Ordinal);
        Assert.True(LicenseKeyGenerator.IsMandantBillingKey(key));
        Assert.False(LicenseKeyGenerator.IsSystemLicenseKey(key));
    }

    [Fact]
    public void GenerateUnifiedLicenseKey_RejectsPastDate()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _generator.GenerateUnifiedLicenseKey(DateTime.UtcNow.AddDays(-1), "cafe"));

        Assert.Equal("validUntil", ex.ParamName);
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("pos")]
    [InlineData("api")]
    [InlineData("www")]
    [InlineData("mail")]
    public void GenerateUnifiedLicenseKey_RejectsReservedSlug(string slug)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _generator.GenerateUnifiedLicenseKey(FutureExpiry, slug));

        Assert.Equal("slug", ex.ParamName);
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatUnifiedLicenseKey_RejectsInvalidRandomPart()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LicenseKeyGenerator.FormatUnifiedLicenseKey(FutureExpiry, "cafe", "SHORT"));

        Assert.Equal("randomPart", ex.ParamName);
    }

    [Fact]
    public void FormatUnifiedLicenseKey_AllowsExpiredDateForWellFormedKeys()
    {
        var past = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var key = LicenseKeyGenerator.FormatUnifiedLicenseKey(past, "cafe", "A7F3K2D9");

        Assert.Equal("REGK-20200101-cafe-A7F3K2D9", key);
        Assert.True(_generator.ValidateLicenseKeyFormat(key));
    }

    [Theory]
    [InlineData("REGK-20261231-dev-A7F3K2D9", true)]
    [InlineData("REGK-20261231-my-cafe-shop-A7F3K2D9", true)]
    [InlineData("REGK-20261231-system-1R61EMER", true)]
    [InlineData("regk-20261231-cafe-a7f3k2d9", true)]
    [InlineData("REGK-20261331-cafe-A7F3K2D9", false)]
    [InlineData("REGK-20261231-admin-A7F3K2D9", false)]
    [InlineData("REGK-20261231-cafe-SHORT01", false)]
    [InlineData("REGK-ABCDE-BBBBB-CCCCC", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateLicenseKeyFormat_MatchesBillingPattern(string? key, bool expected)
    {
        Assert.Equal(expected, _generator.ValidateLicenseKeyFormat(key!));
    }

    [Fact]
    public void GenerateLicenseKey_RejectsEmptySlug()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateLicenseKey("   ", DateTime.UtcNow.AddYears(1)));
    }

    [Fact]
    public void ParseLicenseKey_ReturnsComponentsForValidKey()
    {
        var (slug, validUntil, randomPart) = _generator.ParseLicenseKey("REGK-20261231-dev-A7F3K2D9");

        Assert.Equal("dev", slug);
        Assert.Equal(new DateTime(2026, 12, 31), validUntil);
        Assert.Equal("A7F3K2D9", randomPart);
    }

    [Fact]
    public void ParseLicenseKey_SupportsMultiSegmentSlug()
    {
        var (slug, validUntil, randomPart) =
            _generator.ParseLicenseKey("REGK-20260601-my-cafe-shop-A7F3K2D9");

        Assert.Equal("my-cafe-shop", slug);
        Assert.Equal(new DateTime(2026, 6, 1), validUntil);
        Assert.Equal("A7F3K2D9", randomPart);
    }

    [Fact]
    public void ParseLicenseKey_ReturnsNullsForInvalidKey()
    {
        var (slug, validUntil, randomPart) = _generator.ParseLicenseKey("invalid-key");

        Assert.Null(slug);
        Assert.Null(validUntil);
        Assert.Null(randomPart);
    }

    [Fact]
    public void GenerateLicenseKey_SystemSlug_UsesUnifiedDeploymentShape()
    {
        var key = _generator.GenerateLicenseKey(LicenseKeyGenerator.SystemSlug, FutureExpiry);

        Assert.StartsWith("REGK-20991231-system-", key, StringComparison.Ordinal);
        Assert.True(LicenseKeyGenerator.IsSystemLicenseKey(key));
        Assert.False(LicenseKeyGenerator.IsMandantBillingKey(key));
        Assert.True(LicenseKeyGenerator.IsDeploymentLicenseKey(key));
    }

    [Fact]
    public void IsMandantBillingLicenseKey_RejectsSystemAndLegacyDisplay()
    {
        Assert.True(_generator.IsMandantBillingLicenseKey("REGK-20261231-dev-A7F3K2D9"));
        Assert.False(_generator.IsMandantBillingLicenseKey("REGK-20261231-system-1R61EMER"));
        Assert.False(_generator.IsMandantBillingLicenseKey("REGK-A4WCG-52HL9-66AQI"));
    }
}
