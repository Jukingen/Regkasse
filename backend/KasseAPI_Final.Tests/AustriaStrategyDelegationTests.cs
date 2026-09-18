using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tse;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Proves the Austrian strategies are adapters, not a second engine: every number must come out of
/// <see cref="CartMoneyHelper"/> / <see cref="RksvTaxSetMapper"/> and every side effect must land on the
/// existing service. Full-flow parity stays covered by the CountryBaseline suite.
/// </summary>
public sealed class AustriaStrategyDelegationTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();
    private static readonly AustriaTaxStrategy TaxStrategy = new();

    /// <summary>2 × 2,50 @ 20 % plus 1 × 6,90 @ 10 % — two buckets, both with rounding remainders.</summary>
    private static readonly TaxLineItemInput[] Basket =
    [
        TaxLineItemInput.FromTaxType(2.50m, 2, TaxTypes.Standard),
        TaxLineItemInput.FromTaxType(6.90m, 1, TaxTypes.Reduced),
    ];

    private static TaxCalculationContext Context(bool taxExempt = false) => new()
    {
        CountryProfile = Registry.Get(CountryProfileCodes.Austria),
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        TaxExempt = taxExempt,
    };

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    [Fact]
    public void CalculateTax_ProducesExactlyWhatCartMoneyHelperProduces()
    {
        var expectedLines = new[]
        {
            CartMoneyHelper.ComputeLine(2.50m, 2, TaxTypes.Standard),
            CartMoneyHelper.ComputeLine(6.90m, 1, TaxTypes.Reduced),
        };
        var expectedSummary = CartMoneyHelper.BuildTaxSummaryFromLines(expectedLines);
        var (expectedTotals, _) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown(expectedLines);

        var result = TaxStrategy.CalculateTax(Basket, Context());

        Assert.Equal(expectedLines, result.Lines.ToArray());
        Assert.Equal(expectedSummary.ToArray(), result.TaxSummary.ToArray());
        Assert.Equal(expectedTotals, result.Totals);

        // Serialized comparison: a silently re-derived or re-rounded field would change these strings.
        Assert.Equal(Serialize(expectedLines), Serialize(result.Lines.ToArray()));
        Assert.Equal(Serialize(expectedSummary.ToArray()), Serialize(result.TaxSummary.ToArray()));
        Assert.Equal(Serialize(expectedTotals), Serialize(result.Totals));
    }

    [Fact]
    public void CalculateTax_AcceptsVatPercentLinesLikeTheReceiptPath()
    {
        var expected = CartMoneyHelper.ComputeLine(2.50m, 2, 20m);

        var result = TaxStrategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(2.50m, 2, 20m)],
            Context());

        Assert.Equal(expected, Assert.Single(result.Lines));
    }

    [Fact]
    public void CalculateTax_TaxDetails_MatchTheLivePaymentPathShape()
    {
        var standard = CartMoneyHelper.ComputeLine(2.50m, 2, TaxTypes.Standard);
        var reduced = CartMoneyHelper.ComputeLine(6.90m, 1, TaxTypes.Reduced);

        var result = TaxStrategy.CalculateTax(Basket, Context());

        // Keys are the RKSV tax types as strings, values the summed line VAT — same as payment_details.
        Assert.Equal(2, result.TaxDetails.Count);
        Assert.Equal(standard.LineTax, result.TaxDetails[TaxTypes.Standard.ToString()]);
        Assert.Equal(reduced.LineTax, result.TaxDetails[TaxTypes.Reduced.ToString()]);
    }

    [Fact]
    public void CalculateTax_SumsRepeatedTaxTypesIntoOneBucket()
    {
        var first = CartMoneyHelper.ComputeLine(2.50m, 2, TaxTypes.Standard);
        var second = CartMoneyHelper.ComputeLine(1.20m, 3, TaxTypes.Standard);

        var result = TaxStrategy.CalculateTax(
            [
                TaxLineItemInput.FromTaxType(2.50m, 2, TaxTypes.Standard),
                TaxLineItemInput.FromTaxType(1.20m, 3, TaxTypes.Standard),
            ],
            Context());

        Assert.Equal(first.LineTax + second.LineTax, Assert.Single(result.TaxDetails).Value);
    }

    [Fact]
    public void CalculateTax_IgnoresTaxExempt_SoAustrianOutputCannotDrift()
    {
        // The live payment path has no exemption branch; this package must not add one.
        var withoutExemption = Serialize(TaxStrategy.CalculateTax(Basket, Context(taxExempt: false)));
        var withExemption = Serialize(TaxStrategy.CalculateTax(Basket, Context(taxExempt: true)));

        Assert.Equal(withoutExemption, withExemption);
    }

    [Fact]
    public void CalculateTax_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => TaxStrategy.CalculateTax(null!, Context()));
        Assert.Throws<ArgumentNullException>(() => TaxStrategy.CalculateTax(Basket, null!));
    }

    [Fact]
    public void ProjectFiscalTaxSets_IsByteIdenticalToRksvTaxSetMapper()
    {
        var result = TaxStrategy.CalculateTax(Basket, Context());
        var taxDetailsJson = Serialize(result.TaxDetails);

        var expected = RksvTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, result.Totals.TotalGross);
        var actual = TaxStrategy.ProjectFiscalTaxSets(taxDetailsJson, result.Totals.TotalGross);

        Assert.NotNull(actual);
        Assert.Equal(Serialize(expected), Serialize(actual));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData(null)]
    public void ProjectFiscalTaxSets_KeepsTheMapperFallbackBehaviour(string? taxDetailsJson)
    {
        var expected = RksvTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, 12.34m);

        var actual = TaxStrategy.ProjectFiscalTaxSets(taxDetailsJson, 12.34m);

        Assert.Equal(Serialize(expected), Serialize(actual));
    }

    [Fact]
    public void ValidateVatId_AcceptsTheAustrianUidUnchanged()
    {
        var result = TaxStrategy.ValidateVatId("ATU12345678", Registry.Get(CountryProfileCodes.Austria));

        Assert.True(result.IsValid);
        Assert.Equal("ATU12345678", result.VatId);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ValidateVatId_DelegatesToIVatIdValidator_Once()
    {
        var profile = Registry.Get(CountryProfileCodes.Austria);
        var validator = new Mock<IVatIdValidator>(MockBehavior.Strict);
        validator
            .Setup(v => v.Validate("ATU12345678", profile))
            .Returns(VatIdValidationResult.Valid("ATU12345678"));

        var strategy = new AustriaTaxStrategy(validator.Object);
        var result = strategy.ValidateVatId("ATU12345678", profile);

        Assert.True(result.IsValid);
        Assert.Equal("ATU12345678", result.VatId);
        Assert.Null(result.ErrorCode);
        validator.Verify(v => v.Validate("ATU12345678", profile), Times.Once);
        validator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ATU1234567")]
    [InlineData("DE123456789")]
    [InlineData("ATU123456789")]
    // Strict on purpose: the live fiscal gates reject these, so the strategy must reject them too.
    [InlineData("atu12345678")]
    [InlineData("  ATU12345678  ")]
    public void ValidateVatId_RejectsWhateverTheLiveGatesReject(string? input)
    {
        var result = TaxStrategy.ValidateVatId(input, Registry.Get(CountryProfileCodes.Austria));

        Assert.False(result.IsValid);
        Assert.Null(result.VatId);
        Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, result.ErrorCode);
    }

    [Fact]
    public void DetermineInvoiceFields_ProjectsExistingSettingsWithoutInventingRules()
    {
        var company = new CompanySettings
        {
            CompanyTaxNumber = "ATU12345678",
            Country = "AT",
            BillingCountry = "DE",
            VatRegime = VatRegime.AT_RKSV_STANDARD,
            TaxExempt = true,
        };
        var customer = new Customer { TaxNumber = " ATU99999999 " };

        var fields = TaxStrategy.DetermineInvoiceFields(company, customer);

        Assert.Equal("ATU12345678", fields.SellerVatId);
        Assert.Equal("AT", fields.SellerCountry);
        Assert.Equal("DE", fields.BillingCountry);
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, fields.VatRegime);
        Assert.True(fields.TaxExempt);
        Assert.Equal("ATU99999999", fields.CustomerVatId);

        // Austrian POS receipts never require a customer UID.
        Assert.False(fields.RequiresCustomerVatId);
    }

    [Fact]
    public void DetermineInvoiceFields_LeavesCustomerVatIdNullWhenTheCustomerHasNone()
    {
        var fields = TaxStrategy.DetermineInvoiceFields(new CompanySettings(), new Customer());

        Assert.Null(fields.CustomerVatId);
    }

    [Fact]
    public async Task AllocateReceiptNumber_DelegatesToTheSequenceReservationService()
    {
        const string reserved = "AT-KASSE-BASELINE-01-20260916-7";
        var cashRegisterId = Guid.NewGuid();
        var sequences = new Mock<ISequenceReservationService>();
        sequences
            .Setup(x => x.ReserveNextReceiptNumberAsync(cashRegisterId, It.IsAny<CancellationToken>(), 3))
            .ReturnsAsync(reserved);

        var strategy = new AustriaInvoiceStrategy(sequences.Object, Mock.Of<IReceiptService>());

        var number = await strategy.AllocateReceiptNumberAsync(
            new ReceiptNumberAllocationContext { CashRegisterId = cashRegisterId });

        Assert.Equal(reserved, number);
        sequences.Verify(
            x => x.ReserveNextReceiptNumberAsync(cashRegisterId, It.IsAny<CancellationToken>(), 3),
            Times.Once);
    }

    [Fact]
    public async Task BuildInvoiceDocument_ReturnsTheReceiptServiceOutputUnchanged()
    {
        var payment = new PaymentDetails { Id = Guid.NewGuid(), CashRegisterId = Guid.NewGuid() };
        var expected = new DTOs.ReceiptDTO { ReceiptNumber = "AT-KASSE-BASELINE-01-20260916-2" };
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GenerateReceiptAsync(payment)).ReturnsAsync(expected);

        var strategy = new AustriaInvoiceStrategy(Mock.Of<ISequenceReservationService>(), receipts.Object);

        var document = await strategy.BuildInvoiceDocumentAsync(payment, new CompanySettings(), null);

        Assert.Equal(CountryProfileCodes.Austria, document.CountryCode);
        Assert.Same(expected, document.Receipt);
        receipts.Verify(x => x.GenerateReceiptAsync(payment), Times.Once);
    }

    [Fact]
    public void GetMandatoryDisclosures_NamesTheAustrianReceiptFieldsPinnedByTheBaseline()
    {
        var strategy = new AustriaInvoiceStrategy(
            Mock.Of<ISequenceReservationService>(),
            Mock.Of<IReceiptService>());

        var keys = strategy.GetMandatoryDisclosures(new CompanySettings(), null)
            .Select(d => d.Key)
            .ToArray();

        Assert.Equal(
            [
                "seller.name",
                "seller.address",
                "seller.vatId",
                "receipt.number",
                "receipt.issuedAt",
                "receipt.totalGross",
                "receipt.vatBreakdown",
                "rksv.signature",
            ],
            keys);
    }
}
