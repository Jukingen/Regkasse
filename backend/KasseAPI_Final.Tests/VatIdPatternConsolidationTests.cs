using System.ComponentModel.DataAnnotations;
using System.Reflection;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// The Austrian UID pattern used to be copy-pasted across the payment, tenant-settings, and fiskaly
/// paths. It now has one literal (<see cref="VatIdPatterns.Austria"/>) that the CountryProfile seed and
/// the DataAnnotation attributes share. These tests keep it that way and pin the **strict** semantics —
/// no trimming, no case folding — so the consolidation cannot loosen a fiscal gate.
/// </summary>
public sealed class VatIdPatternConsolidationTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    [Fact]
    public void AustrianSeed_UsesTheSharedConstant()
    {
        Assert.Equal(VatIdPatterns.Austria, Registry.Get(CountryProfileCodes.Austria).VatIdPattern);
        Assert.Equal(VatIdPatterns.Germany, Registry.Get(CountryProfileCodes.Germany).VatIdPattern);
        Assert.Equal(VatIdPatterns.Switzerland, Registry.Get(CountryProfileCodes.Switzerland).VatIdPattern);
        Assert.Equal(VatIdPatterns.EuDefault, Registry.Get(CountryProfileCodes.EuDefault).VatIdPattern);
    }

    [Fact]
    public void SharedPattern_IsStillTheContractedAustrianShape()
    {
        // The literal itself is the contract with the fiscal path; changing it is a breaking change.
        Assert.Equal(@"^ATU\d{8}$", VatIdPatterns.Austria);
    }

    [Theory]
    [InlineData("ATU12345678", true)]
    [InlineData("ATU00000000", true)]
    [InlineData("ATU1234567", false)]
    [InlineData("ATU123456789", false)]
    [InlineData("ATU1234567X", false)]
    [InlineData("DE123456789", false)]
    [InlineData("atu12345678", false)]
    [InlineData(" ATU12345678", false)]
    [InlineData("ATU12345678 ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAustrianUid_IsStrict(string? vatId, bool expected)
    {
        Assert.Equal(expected, VatIdPatterns.IsAustrianUid(vatId));
    }

    [Theory]
    [InlineData("ATU12345678", true)]
    [InlineData("atu12345678", false)]
    [InlineData("  ATU12345678  ", false)]
    [InlineData("ATU1234567X", false)]
    public void ProfileShapeCheck_AgreesWithTheSharedHelper(string vatId, bool expected)
    {
        var profile = Registry.Get(CountryProfileCodes.Austria);

        Assert.Equal(expected, profile.MatchesVatIdShape(vatId));
        Assert.Equal(VatIdPatterns.IsAustrianUid(vatId), profile.MatchesVatIdShape(vatId));
    }

    [Theory]
    [InlineData(typeof(PaymentDetails), nameof(PaymentDetails.Steuernummer))]
    [InlineData(typeof(CreatePaymentRequest), nameof(CreatePaymentRequest.Steuernummer))]
    public void DataAnnotations_ReferenceTheSharedConstant(Type declaringType, string propertyName)
    {
        // Attributes need a compile-time constant, so they cannot read the registry. They must at least
        // point at the same literal, otherwise model binding and the fiscal gate can drift apart.
        var attribute = declaringType
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<RegularExpressionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(VatIdPatterns.Austria, attribute!.Pattern);
    }

    [Fact]
    public void NoProductionFileDeclaresItsOwnAustrianUidRegex()
    {
        var backendRoot = FindBackendRoot();
        var offenders = Directory
            .EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains("KasseAPI_Final.Tests", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("VatIdPatterns.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(@"^ATU\d{8}$", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
