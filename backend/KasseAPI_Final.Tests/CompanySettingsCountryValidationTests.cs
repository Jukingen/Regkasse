using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models;
using Xunit;

// KasseAPI_Final.Controllers also declares a ValidationResult.
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Server-side validation for the optional billing country. VAT-ID is deliberately not validated here —
/// it stays on <c>CompanyTaxNumber</c> and its pattern belongs to the country profile work.
/// </summary>
public sealed class CompanySettingsCountryValidationTests
{
    [Theory]
    [InlineData("AT")]
    [InlineData("DE")]
    [InlineData("ch")]
    [InlineData(" LI ")]
    public void TryNormalize_AcceptsTwoLetterCodesAndUpperCasesThem(string input)
    {
        Assert.True(Iso3166CountryCode.TryNormalize(input, out var normalized));
        Assert.Equal(input.Trim().ToUpperInvariant(), normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_TreatsBlankAsCleared(string? input)
    {
        Assert.True(Iso3166CountryCode.TryNormalize(input, out var normalized));
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AUT")]
    [InlineData("A1")]
    [InlineData("12")]
    [InlineData("A-")]
    [InlineData("ÖS")]
    public void TryNormalize_RejectsMalformedCodes(string input)
    {
        Assert.False(Iso3166CountryCode.TryNormalize(input, out var normalized));
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("AT", true)]
    [InlineData("at", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("AUT", false)]
    public void IsValid_ChecksTheTwoLetterShape(string? input, bool expected)
    {
        Assert.Equal(expected, Iso3166CountryCode.IsValid(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AT")]
    [InlineData("de")]
    public void UpdateRequest_AcceptsOmittedBlankAndValidBillingCountry(string? billingCountry)
    {
        var request = CreateValidRequest();
        request.BillingCountry = billingCountry;

        Assert.DoesNotContain(Validate(request), r => r.MemberNames.Contains(nameof(request.BillingCountry)));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AUT")]
    [InlineData("A1")]
    public void UpdateRequest_RejectsMalformedBillingCountry(string billingCountry)
    {
        var request = CreateValidRequest();
        request.BillingCountry = billingCountry;

        var failures = Validate(request)
            .Where(r => r.MemberNames.Contains(nameof(request.BillingCountry)))
            .ToList();

        Assert.NotEmpty(failures);
    }

    [Fact]
    public void UpdateRequest_LeavesCountryFieldsOptional()
    {
        var request = CreateValidRequest();

        Assert.Null(request.BillingCountry);
        Assert.Null(request.VatRegime);
        Assert.Null(request.TaxExempt);
        Assert.Empty(Validate(request));
    }

    [Fact]
    public void UpdateRequest_DoesNotExposeAnOperatingCountryOrVatId()
    {
        var properties = typeof(UpdateCompanySettingsRequest).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains(nameof(UpdateCompanySettingsRequest.CompanyTaxNumber), properties);
        Assert.Contains(nameof(UpdateCompanySettingsRequest.BillingCountry), properties);
        Assert.DoesNotContain("Country", properties);
        Assert.DoesNotContain("CountryCode", properties);
        Assert.DoesNotContain("VatId", properties);
    }

    private static List<DataAnnotationsValidationResult> Validate(UpdateCompanySettingsRequest request)
    {
        var results = new List<DataAnnotationsValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static UpdateCompanySettingsRequest CreateValidRequest() => new()
    {
        CompanyName = "Baseline Gastro GmbH",
        CompanyAddress = "Baselinegasse 1, 1010 Wien",
        CompanyTaxNumber = "ATU12345678",
        BusinessHours = new Dictionary<string, string>(),
        DefaultCurrency = "EUR",
        DefaultLanguage = "de-DE",
        DefaultTimeZone = "Europe/Vienna",
        DefaultDateFormat = "dd.MM.yyyy",
        DefaultTimeFormat = "HH:mm:ss",
        DefaultDecimalPlaces = 2,
        TaxCalculationMethod = "Standard",
        InvoiceNumbering = "Sequential",
        ReceiptNumbering = "Sequential",
        DefaultPaymentMethod = "Cash",
    };
}
