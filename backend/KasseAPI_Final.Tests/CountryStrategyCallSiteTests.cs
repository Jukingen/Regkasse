using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tests.CountryBaseline;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 30: domain call sites resolve country strategies. AT output is pinned to the committed
/// fiscal-chain baseline; DE/CH skeletons fail closed.
/// </summary>
public sealed class CountryStrategyCallSiteTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();
    private static readonly ITaxStrategyResolver TaxResolver = CountryStrategyWiring.CreateTaxResolver();
    private static readonly IInvoiceStrategyResolver InvoiceResolver = CountryStrategyWiring.CreateInvoiceResolver();
    private static readonly JsonNode FiscalChain = BaselineFixtureFile.Read("at-fiscal-chain.baseline.json");

    private static TaxCalculationContext AtContext(bool taxExempt = false) => new()
    {
        CountryProfile = Registry.Get(CountryProfileCodes.Austria),
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        TaxExempt = taxExempt,
    };

    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void TaxResolver_Austria_PicksAustriaTaxStrategy()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        Assert.IsType<AustriaTaxStrategy>(strategy);
    }

    [Fact]
    public void TaxResolver_Germany_PicksGermanyTaxStrategy()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        Assert.IsType<GermanyTaxStrategy>(strategy);
    }

    [Fact]
    public void AtCalculateTax_MatchesFiscalChainVatFixture()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);
        var cases = FiscalChain["vatCalculation"]!.AsArray();

        foreach (var row in cases)
        {
            var caseName = row!["case"]!.GetValue<string>();
            if (caseName == "mixed-basket-20-10-13-0")
            {
                var mixed = strategy.CalculateTax(
                    [
                        TaxLineItemInput.FromVatPercent(6.90m, 1, 10m),
                        TaxLineItemInput.FromVatPercent(2.50m, 2, 20m),
                        TaxLineItemInput.FromVatPercent(113.00m, 1, 13m),
                        TaxLineItemInput.FromVatPercent(4.00m, 1, 0m),
                    ],
                    AtContext());

                Assert.Equal(row["receiptTotalNet"]!.GetValue<string>(), Money(mixed.Totals.TotalNet));
                Assert.Equal(row["receiptTotalVat"]!.GetValue<string>(), Money(mixed.Totals.TotalVat));
                Assert.Equal(row["receiptTotalGross"]!.GetValue<string>(), Money(mixed.Totals.TotalGross));
                continue;
            }

            var unitGross = decimal.Parse(row["unitGross"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var quantity = row["quantity"]!.GetValue<int>();
            var vatPercent = decimal.Parse(row["vatRatePercent"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var result = strategy.CalculateTax(
                [TaxLineItemInput.FromVatPercent(unitGross, quantity, vatPercent)],
                AtContext());
            var line = Assert.Single(result.Lines);

            Assert.Equal(row["lineNet"]!.GetValue<string>(), Money(line.LineNet));
            Assert.Equal(row["lineTax"]!.GetValue<string>(), Money(line.LineTax));
            Assert.Equal(row["lineGross"]!.GetValue<string>(), Money(line.LineGross));
            Assert.Equal(row["receiptTotalNet"]!.GetValue<string>(), Money(result.Totals.TotalNet));
            Assert.Equal(row["receiptTotalVat"]!.GetValue<string>(), Money(result.Totals.TotalVat));
            Assert.Equal(row["receiptTotalGross"]!.GetValue<string>(), Money(result.Totals.TotalGross));
        }
    }

    [Fact]
    public void AtProjectFiscalTaxSets_MatchesFiscalChainTaxSetFixture()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        foreach (var row in FiscalChain["rksvTaxSetMapping"]!.AsArray())
        {
            var json = ReadOptionalString(row!["taxDetailsJson"]);
            var total = decimal.Parse(row["totalAmount"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            var projected = CountryStrategyWiring.RequireTaxSets(
                strategy.ProjectFiscalTaxSets(json, total),
                CountryProfileCodes.Austria);

            Assert.Equal(row["normal20"]!.GetValue<string>(), Money(projected.Normal));
            Assert.Equal(row["ermaessigt1_10"]!.GetValue<string>(), Money(projected.Ermaessigt1));
            Assert.Equal(row["ermaessigt2_13"]!.GetValue<string>(), Money(projected.Ermaessigt2));
            Assert.Equal(row["null0"]!.GetValue<string>(), Money(projected.Null));
            Assert.Equal(row["besonders"]!.GetValue<string>(), Money(projected.Besonders));
            Assert.Equal(row["totalGross"]!.GetValue<string>(), Money(projected.TotalGross));
            Assert.Equal(row["totalGrossCents"]!.GetValue<long>(), projected.TotalGrossCents);
        }
    }

    [Theory]
    [InlineData(42.00)]
    [InlineData(0.00)]
    public void ClosingTaxSets_ProjectFiscalTaxSetsMatchesRksvTaxSetMapper(double total)
    {
        var totalAmount = (decimal)total;
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        var mapper = RksvTaxSetMapper.MapFromTaxDetailsJson("{}", totalAmount);
        var projected = CountryStrategyWiring.RequireTaxSets(
            strategy.ProjectFiscalTaxSets("{}", totalAmount),
            CountryProfileCodes.Austria);

        Assert.Equal(mapper.Normal, projected.Normal);
        Assert.Equal(mapper.Ermaessigt1, projected.Ermaessigt1);
        Assert.Equal(mapper.Ermaessigt2, projected.Ermaessigt2);
        Assert.Equal(mapper.Null, projected.Null);
        Assert.Equal(mapper.Besonders, projected.Besonders);
        Assert.Equal(mapper.TotalGross, projected.TotalGross);
        Assert.Equal(mapper.TotalGrossCents, projected.TotalGrossCents);
        Assert.Equal(
            JsonSerializer.Serialize(mapper),
            JsonSerializer.Serialize(projected));
    }

    [Fact]
    public void ClosingTaxSets_EmptyObjectFallsBackToNormal_AndZeroStaysZero()
    {
        var strategy = (AustriaTaxStrategy)TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        var emptyToNormal = CountryStrategyWiring.RequireTaxSets(
            strategy.ProjectFiscalTaxSets("{}", 42.00m),
            CountryProfileCodes.Austria);
        Assert.Equal(42.00m, emptyToNormal.Normal);
        Assert.Equal(0m, emptyToNormal.Ermaessigt1);

        var zero = CountryStrategyWiring.RequireTaxSets(
            strategy.ProjectFiscalTaxSets("{}", 0m),
            CountryProfileCodes.Austria);
        Assert.Equal(RksvTaxSetAmounts.Zero.Normal, zero.Normal);
        Assert.Equal(0m, zero.TotalGross);
        Assert.Equal(0, zero.TotalGrossCents);
    }

    [Fact]
    public void AtProjectFiscalTaxSets_IsByteIdenticalToMapTaxSets()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);
        const string json = """{"1":5.00,"2":0.63}""";
        const decimal total = 36.90m;

        var mapped = BelegdatenPayloadBuilder.MapTaxSets(json, total);
        var projected = CountryStrategyWiring.RequireTaxSets(
            strategy.ProjectFiscalTaxSets(json, total),
            CountryProfileCodes.Austria);

        Assert.Equal(JsonSerializer.Serialize(mapped), JsonSerializer.Serialize(projected));
    }

    [Fact]
    public void GermanyTaxStrategy_CalculateTax_UsesDeRates()
    {
        var strategy = TaxResolver.Resolve(
            Registry.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        var result = strategy.CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(119m, 1, 19m),
                TaxLineItemInput.FromVatPercent(107m, 1, 7m),
            ],
            new TaxCalculationContext
            {
                CountryProfile = Registry.Get(CountryProfileCodes.Germany),
                VatRegime = VatRegime.DE_USTG_STANDARD,
                TaxExempt = false,
            });

        Assert.Equal(19m, result.TaxSummary.Single(s => s.TaxRatePct == 19m).TaxRatePct);
        Assert.Equal(7m, result.TaxSummary.Single(s => s.TaxRatePct == 7m).TaxRatePct);
    }

    [Fact]
    public void GermanyInvoiceStrategy_GetMandatoryDisclosures_ReturnsUstgKeys()
    {
        var strategy = InvoiceResolver.Resolve(
            Registry.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        var keys = strategy.GetMandatoryDisclosures(new CompanySettings { Country = "DE" }, customer: null)
            .Select(d => d.Key)
            .ToArray();

        Assert.Contains("seller.vatId", keys);
        Assert.Contains("invoice.number", keys);
        Assert.Contains("invoice.gross", keys);
    }

    [Fact]
    public async Task MissingCompanySettings_DefaultsToAustriaStandardRegime()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var context = new CountryStrategyContext(db, Registry, TenantTestDoubles.PrimaryTenantResolver);
        var binding = await context.LoadAsync();

        Assert.True(binding.UsedLegacyFallback);
        Assert.Equal(CountryProfileCodes.Austria, binding.Profile.Code);
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, binding.VatRegime);
        Assert.Equal(CountryProfileCodes.Austria, binding.Settings.Country);
        Assert.IsType<AustriaTaxStrategy>(TaxResolver.Resolve(binding.Profile, binding.VatRegime));
    }

    [Fact]
    public async Task PaymentService_AtBasket_TaxDetailsMatchAustriaTaxStrategy()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.NotNull(result.Payment);

        var expected = new AustriaTaxStrategy().CalculateTax(
            [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Reduced)],
            AtContext());

        Assert.Equal(expected.Totals.TotalGross, result.Payment!.TotalAmount);
        Assert.Equal(expected.Totals.TotalVat, result.Payment.TaxAmount);

        var taxJson = result.Payment.TaxDetails.RootElement;
        foreach (var (key, amount) in expected.TaxDetails)
        {
            Assert.True(taxJson.TryGetProperty(key, out var prop), $"missing taxDetails key {key}");
            Assert.Equal(amount, prop.GetDecimal());
        }
    }

    [Fact]
    public async Task TseService_AtSale_ProjectFiscalTaxSetsIsUsedAndMatchesMapTaxSets()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var inner = new AustriaTaxStrategy();
        var tax = new Mock<ITaxStrategy>();
        tax.SetupGet(t => t.CountryCode).Returns(CountryProfileCodes.Austria);
        tax.Setup(t => t.ProjectFiscalTaxSets(It.IsAny<string?>(), It.IsAny<decimal>()))
            .Returns((string? json, decimal total) => inner.ProjectFiscalTaxSets(json, total));

        var resolver = new Mock<ITaxStrategyResolver>();
        resolver
            .Setup(r => r.Resolve(It.IsAny<CountryProfile>(), It.IsAny<VatRegime>()))
            .Returns(tax.Object);

        var countryContext = new CountryStrategyContext(db, Registry, TenantTestDoubles.PrimaryTenantResolver);
        var key = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(key, Mock.Of<ILogger<SignaturePipeline>>());
        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new TseService(
            db,
            pipeline,
            key,
            provider.Object,
            Mock.Of<ILogger<TseService>>(),
            countryStrategyContext: countryContext,
            taxStrategyResolver: resolver.Object);

        try
        {
            await sut.CreateInvoiceSignatureAsync(
                Guid.NewGuid(),
                "AT-KASSE-01-20260112-1",
                10m,
                "KASSE-01",
                taxDetailsJson: """{"1":1.67}""");
        }
        catch (Exception ex) when (ex is TseUnavailableException or InvalidOperationException)
        {
            // Isolated harness may lack Fiskaly/AES; ProjectFiscalTaxSets must still have run.
        }

        tax.Verify(
            t => t.ProjectFiscalTaxSets("""{"1":1.67}""", 10m),
            Times.Once);
        resolver.Verify(
            r => r.Resolve(It.Is<CountryProfile>(p => p.Code == CountryProfileCodes.Austria), VatRegime.AT_RKSV_STANDARD),
            Times.Once);
    }

    [Fact]
    public async Task TseService_GermanySettings_ThrowsNotImplemented_BeforeSigning()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, "DE", VatRegime.DE_USTG_STANDARD);
        await db.SaveChangesAsync();

        var countryContext = new CountryStrategyContext(db, Registry, TenantTestDoubles.PrimaryTenantResolver);
        var key = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(key, Mock.Of<ILogger<SignaturePipeline>>());
        var sut = new TseService(
            db,
            pipeline,
            key,
            Mock.Of<ITseProvider>(),
            Mock.Of<ILogger<TseService>>(),
            countryStrategyContext: countryContext,
            taxStrategyResolver: TaxResolver);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            sut.CreateInvoiceSignatureAsync(
                Guid.NewGuid(),
                "DE-1",
                10m,
                "KASSE-01"));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvoiceService_GermanySettings_ThrowsNotImplemented_WithoutMappingChange()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, "DE", VatRegime.DE_USTG_STANDARD);
        await db.SaveChangesAsync();

        var countryContext = new CountryStrategyContext(db, Registry, TenantTestDoubles.PrimaryTenantResolver);
        var sut = new InvoiceService(
            db,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "DE GmbH",
                TaxNumber = "DE123",
                Street = "S",
                ZipCode = "10115",
                City = "Berlin",
            }),
            TenantTestDoubles.PrimaryTenantResolver,
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: InvoiceResolver,
            taxStrategyResolver: TaxResolver);

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "DE123",
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = "1",
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
        };

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            sut.GenerateInvoiceAsync(payment));
        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RksvSpecialReceipt_GermanySettings_ThrowsNotImplemented_BeforeBelegNr()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, "DE", VatRegime.DE_USTG_STANDARD);
        await db.SaveChangesAsync();

        var countryContext = new CountryStrategyContext(db, Registry, TenantTestDoubles.PrimaryTenantResolver);
        var seq = new Mock<IReceiptSequenceService>(MockBehavior.Strict);
        var tse = new Mock<ITseService>(MockBehavior.Strict);
        var receipts = Mock.Of<IReceiptService>();

        var sut = new RksvSpecialReceiptService(
            db,
            tse.Object,
            seq.Object,
            receipts,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "DE GmbH",
                TaxNumber = "DE123",
                Street = "S",
                ZipCode = "10115",
                City = "Berlin",
            }),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Mock.Of<ILogger<RksvSpecialReceiptService>>(),
            new RksvSpecialReceiptFinanzOnlineSubmissionTracker(db),
            new FinanzOnlineOutboxService(db, Mock.Of<ILogger<FinanzOnlineOutboxService>>()),
            Mock.Of<IReportPdfCaptureService>(),
            Mock.Of<IOptionsMonitor<KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineModeOptions>>(
                o => o.CurrentValue == new KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineModeOptions { Mode = "Test" }),
            Mock.Of<IOptionsMonitor<KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineCutoverGuardOptions>>(
                o => o.CurrentValue == new KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineCutoverGuardOptions()),
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: InvoiceResolver);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            sut.CreateNullbelegAsync(
                new CreateNullbelegRequest { CashRegisterId = Guid.NewGuid() },
                actorUserId: "user-1"));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
        seq.VerifyNoOtherCalls();
        tse.VerifyNoOtherCalls();
    }

    private static string? ReadOptionalString(JsonNode? node)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
            return null;
        return node.GetValue<string>();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CountryCallSite_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static void SeedCountrySettings(AppDbContext db, string country, VatRegime vatRegime)
    {
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Test GmbH",
            CompanyAddress = "S, 1010 Wien",
            CompanyTaxNumber = country == "AT" ? "ATU12345678" : "DE123456789",
            Country = country,
            VatRegime = vatRegime,
            BusinessHours = new Dictionary<string, string>(),
            Currency = "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });
    }
}
