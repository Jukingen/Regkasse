using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Country-profile VAT-ID engine. Shape comes from the seed regex; VIES is opt-in and mocked.
/// </summary>
public sealed class VatIdValidatorTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    private static CountryProfile At => Registry.Get(CountryProfileCodes.Austria);
    private static CountryProfile De => Registry.Get(CountryProfileCodes.Germany);
    private static CountryProfile Ch => Registry.Get(CountryProfileCodes.Switzerland);
    private static CountryProfile EuDefault => Registry.Get(CountryProfileCodes.EuDefault);

    private static VatIdValidator ShapeOnly() => new(new DisabledViesClient());

    [Fact]
    public void At_ValidUid_PassesUnchanged()
    {
        var result = ShapeOnly().Validate("ATU12345678", At);

        Assert.True(result.IsValid);
        Assert.Equal("ATU12345678", result.VatId);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void At_TooShort_FailsWithShapeCode()
    {
        var result = ShapeOnly().Validate("ATU1234567", At);

        Assert.False(result.IsValid);
        Assert.Null(result.VatId);
        Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, result.ErrorCode);
    }

    [Theory]
    [InlineData("DE123456789")]
    [InlineData("DE12345678")]
    public void De_Shape_MatchesTheGermanSeed(string vatId)
    {
        var expected = De.MatchesVatIdShape(vatId);
        var result = ShapeOnly().Validate(vatId, De);

        Assert.Equal(expected, result.IsValid);
        if (expected)
        {
            Assert.Equal(vatId, result.VatId);
            Assert.Null(result.ErrorCode);
        }
        else
        {
            Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, result.ErrorCode);
        }
    }

    [Fact]
    public void De_NineDigits_Passes()
    {
        var result = ShapeOnly().Validate("DE123456789", De);

        Assert.True(result.IsValid);
        Assert.Equal("DE123456789", result.VatId);
    }

    [Fact]
    public void De_EightDigits_Fails()
    {
        var result = ShapeOnly().Validate("DE12345678", De);

        Assert.False(result.IsValid);
        Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, result.ErrorCode);
    }

    [Theory]
    [InlineData("CHE-123.456.789", true)]
    [InlineData("CHE-123.456.789 MWST", true)]
    [InlineData("CHE-123.456.78", false)]
    public void Ch_Shape_MatchesTheSwissSeed(string vatId, bool expected)
    {
        var result = ShapeOnly().Validate(vatId, Ch);

        Assert.Equal(expected, result.IsValid);
        if (expected)
            Assert.Equal(vatId, result.VatId);
        else
            Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, result.ErrorCode);
    }

    [Fact]
    public void Normalize_TrimsAndUppercases_WithoutStrippingPrefix()
    {
        var validator = ShapeOnly();

        Assert.Equal("ATU12345678", validator.Normalize("  atu12345678  ", At));
        Assert.Equal("DE123456789", validator.Normalize("de123456789", De));
        Assert.Equal("CHE-123.456.789 MWST", validator.Normalize(" che-123.456.789 mwst ", Ch));
    }

    [Fact]
    public void Validate_DoesNotNormalize_SoLiveGatesStayStrict()
    {
        var validator = ShapeOnly();

        Assert.False(validator.Validate("atu12345678", At).IsValid);
        Assert.False(validator.Validate("  ATU12345678  ", At).IsValid);
    }

    [Fact]
    public async Task Vies_DisabledByDefault_DoesNotCallClient()
    {
        var client = new Mock<IViesClient>(MockBehavior.Strict);
        var validator = new VatIdValidator(client.Object);

        var result = await validator.CheckViesAsync("ATU12345678", At);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
        client.Verify(
            c => c.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Vies_EnabledForEu_CallsClientWithSplitWireFormat()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>(MockBehavior.Strict);
        client
            .Setup(c => c.LookupAsync("AT", "U12345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViesLookupResult.Valid());
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("ATU12345678", At);

        Assert.True(result.IsValid);
        client.Verify(
            c => c.LookupAsync("AT", "U12345678", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Vies_EnabledForDe_CallsClientWithNationalNumber()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>(MockBehavior.Strict);
        client
            .Setup(c => c.LookupAsync("DE", "123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViesLookupResult.Valid());
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("DE123456789", De);

        Assert.True(result.IsValid);
        client.Verify(
            c => c.LookupAsync("DE", "123456789", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Vies_EnabledForCh_DoesNotCallClient()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>(MockBehavior.Strict);
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("CHE-123.456.789", Ch);

        Assert.True(result.IsValid);
        client.Verify(
            c => c.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Vies_EnabledForEuDefault_DoesNotCallClient()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>(MockBehavior.Strict);
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("FR12345678901", EuDefault);

        Assert.True(result.IsValid);
        client.Verify(
            c => c.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Vies_EnabledAndMockInvalid_ReturnsViesInvalid()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>();
        client
            .Setup(c => c.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViesLookupResult.Invalid());
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("ATU12345678", At);

        Assert.Equal(ViesLookupStatus.Invalid, result.Status);
        Assert.Equal(ViesLookupResult.InvalidErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task Vies_EnabledAndMockUnavailable_ReturnsViesUnavailable()
    {
        var flags = EnabledViesFlags();
        var client = new Mock<IViesClient>();
        client
            .Setup(c => c.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViesLookupResult.Unavailable());
        var validator = new VatIdValidator(client.Object, flags.Object);

        var result = await validator.CheckViesAsync("ATU12345678", At);

        Assert.Equal(ViesLookupStatus.Unavailable, result.Status);
        Assert.Equal(ViesLookupResult.UnavailableErrorCode, result.ErrorCode);
    }

    private static Mock<IFeatureFlagService> EnabledViesFlags()
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        return flags;
    }
}
