using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CountryCallSiteMigrationTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry Rates = new CountryTaxTypeRegistry();
    private static readonly ITaxStrategyResolver TaxResolver = CountryStrategyWiring.CreateTaxResolver();
    private static readonly IInvoiceStrategyResolver InvoiceResolver = CountryStrategyWiring.CreateInvoiceResolver();

    private static TaxCalculationContext AtContext() => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Austria),
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        TaxExempt = false,
    };

    [Fact]
    public void Mapper_Austria_KeepsFromTaxType()
    {
        var line = CountryPaymentTaxLineMapper.FromProductTaxType(
            Profiles.Get(CountryProfileCodes.Austria),
            10m,
            1,
            TaxTypes.Reduced,
            Rates);

        Assert.Equal(TaxTypes.Reduced, line.TaxType);
        Assert.Null(line.VatRatePercent);
    }

    [Theory]
    [InlineData(TaxTypes.Standard, 19)]
    [InlineData(TaxTypes.Reduced, 7)]
    [InlineData(TaxTypes.ZeroRate, 0)]
    public void Mapper_Germany_ReinterpretsRksvTaxTypeInts(int rksvTaxType, int expectedPercent)
    {
        var line = CountryPaymentTaxLineMapper.FromProductTaxType(
            Profiles.Get(CountryProfileCodes.Germany),
            119m,
            1,
            rksvTaxType,
            Rates);

        Assert.Equal(expectedPercent, line.VatRatePercent);
        Assert.Null(line.TaxType);
    }

    [Theory]
    [InlineData(TaxTypes.Standard, 8.1)]
    [InlineData(TaxTypes.Reduced, 2.6)]
    [InlineData(TaxTypes.Special, 3.8)]
    public void Mapper_Switzerland_ReinterpretsRksvTaxTypeInts(int rksvTaxType, double expectedPercent)
    {
        var line = CountryPaymentTaxLineMapper.FromProductTaxType(
            Profiles.Get(CountryProfileCodes.Switzerland),
            108.1m,
            1,
            rksvTaxType,
            Rates);

        Assert.Equal((decimal)expectedPercent, line.VatRatePercent);
    }

    [Fact]
    public void Mapper_Germany_SpecialAndReducedNew_Throw()
    {
        var de = Profiles.Get(CountryProfileCodes.Germany);
        Assert.Throws<ArgumentException>(() =>
            CountryPaymentTaxLineMapper.FromProductTaxType(de, 10m, 1, TaxTypes.Special, Rates));
        Assert.Throws<ArgumentException>(() =>
            CountryPaymentTaxLineMapper.FromProductTaxType(de, 10m, 1, TaxTypes.ReducedNew, Rates));
    }

    [Fact]
    public async Task AtPaymentThenInvoice_MatchesAustriaTaxStrategy()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var pay = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await pay.CreatePaymentAsync(
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

        var invoices = new InvoiceService(
            ctx,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "Test GmbH",
                TaxNumber = "ATU12345678",
                Street = "S1",
                ZipCode = "1010",
                City = "Wien",
            }),
            TenantTestDoubles.PrimaryTenantResolver,
            countryStrategyContext: new CountryStrategyContext(ctx, Profiles, TenantTestDoubles.PrimaryTenantResolver),
            invoiceStrategyResolver: InvoiceResolver,
            taxStrategyResolver: TaxResolver);

        var dto = await invoices.GenerateInvoiceAsync(result.Payment);
        Assert.Equal(expected.Totals.TotalGross, dto.TotalAmount);
        Assert.Equal(expected.Totals.TotalVat, dto.TaxAmount);
        Assert.Equal(expected.Totals.TotalNet, dto.Subtotal);
    }

    [Fact]
    public async Task DeInvoice_MapsStructured_AndRksvThrowsNotSupported()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD, "DE123456789");
        await db.SaveChangesAsync();

        var countryContext = new CountryStrategyContext(db, Profiles, TenantTestDoubles.PrimaryTenantResolver);
        var invoices = new InvoiceService(
            db,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "DE GmbH",
                TaxNumber = "DE123456789",
                Street = "S",
                ZipCode = "10115",
                City = "Berlin",
            }),
            TenantTestDoubles.PrimaryTenantResolver,
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: InvoiceResolver,
            taxStrategyResolver: TaxResolver);

        var dto = await invoices.GenerateInvoiceAsync(Payment("DE123456789", 119m, 19m, "RE-DE-1"));
        Assert.Equal("RE-DE-1", dto.InvoiceNumber);
        Assert.Equal(100m, dto.Subtotal);
        Assert.Equal(19m, dto.TaxAmount);
        Assert.Equal(119m, dto.TotalAmount);
        Assert.Equal("DE123456789", dto.SellerTaxNumber);

        var rksvEx = await Assert.ThrowsAsync<NotSupportedException>(() =>
            CreateRksv(db, countryContext).CreateNullbelegAsync(
                new CreateNullbelegRequest { CashRegisterId = Guid.NewGuid() },
                actorUserId: "user-1"));
        Assert.Contains("Austria-only", rksvEx.Message, StringComparison.Ordinal);
        Assert.Contains("DE", rksvEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChInvoice_MapsStructured_AndRksvThrowsNotSupported()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, CountryProfileCodes.Switzerland, VatRegime.CH_MWST_STANDARD, "CHE-123.456.789 MWST");
        await db.SaveChangesAsync();

        var countryContext = new CountryStrategyContext(db, Profiles, TenantTestDoubles.PrimaryTenantResolver);
        var invoices = new InvoiceService(
            db,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "CH GmbH",
                TaxNumber = "CHE-123.456.789 MWST",
                Street = "S",
                ZipCode = "8001",
                City = "Zürich",
            }),
            TenantTestDoubles.PrimaryTenantResolver,
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: InvoiceResolver,
            taxStrategyResolver: TaxResolver);

        var dto = await invoices.GenerateInvoiceAsync(Payment("CHE-123.456.789 MWST", 108.1m, 8.1m, "RE-CH-1"));
        Assert.Equal("RE-CH-1", dto.InvoiceNumber);
        Assert.Equal(100m, dto.Subtotal);
        Assert.Equal(8.1m, dto.TaxAmount);
        Assert.Equal(108.1m, dto.TotalAmount);
        Assert.Equal("CHE-123.456.789 MWST", dto.SellerTaxNumber);

        var rksvEx = await Assert.ThrowsAsync<NotSupportedException>(() =>
            CreateRksv(db, countryContext).CreateNullbelegAsync(
                new CreateNullbelegRequest { CashRegisterId = Guid.NewGuid() },
                actorUserId: "user-1"));
        Assert.Contains("CH", rksvEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EuReverseChargePayment_IsZeroRated_WhenBuyerVatIdValid()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, _, registerId, categoryId) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 121m);
        var productId = await PaymentServiceCoverageHarness.AddProductAsync(
            ctx,
            categoryId,
            "EU item",
            121m,
            TaxTypes.Standard);
        SeedCountrySettings(ctx, CountryProfileCodes.EuDefault, VatRegime.EU_REVERSE_CHARGE, "FR12345678901");
        await ctx.SaveChangesAsync();

        var pay = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                CompanyProfile = new CompanyProfileOptions
                {
                    CompanyName = "EU GmbH",
                    TaxNumber = "FR12345678901",
                    Street = "S",
                    ZipCode = "1000",
                    City = "Brussels",
                },
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 121m);
        request.Steuernummer = "FR12345678901";
        request.Items[0].TaxType = TaxType.Standard;

        var result = await pay.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(121m, result.Payment!.TotalAmount);
        Assert.Equal(0m, result.Payment.TaxAmount);
        Assert.Equal(0m, result.Payment.TaxDetails.RootElement.GetProperty("0").GetDecimal());
    }

    [Fact]
    public async Task DeInvoice_FlagOff_ThrowsFeatureDisabled_AtCallSite()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        SeedCountrySettings(db, CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD, "DE123456789");
        await db.SaveChangesAsync();

        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(false);

        var invoiceResolver = new InvoiceStrategyResolver(
        [
            new AustriaInvoiceStrategy(Mock.Of<ISequenceReservationService>(), Mock.Of<IReceiptService>()),
            new GermanyInvoiceStrategy(flags.Object),
            new SwitzerlandInvoiceStrategy(),
            new EuDefaultInvoiceStrategy(),
        ]);

        var invoices = new InvoiceService(
            db,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "DE GmbH",
                TaxNumber = "DE123456789",
                Street = "S",
                ZipCode = "10115",
                City = "Berlin",
            }),
            TenantTestDoubles.PrimaryTenantResolver,
            countryStrategyContext: new CountryStrategyContext(db, Profiles, TenantTestDoubles.PrimaryTenantResolver),
            invoiceStrategyResolver: invoiceResolver,
            taxStrategyResolver: TaxResolver);

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            invoices.GenerateInvoiceAsync(Payment("DE123456789", 119m, 19m, "RE-DE-OFF")));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }

    private static PaymentDetails Payment(string vatId, decimal gross, decimal tax, string number) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = "Guest",
        CashierId = "c1",
        TotalAmount = gross,
        TaxAmount = tax,
        PaymentMethodRaw = "0",
        Steuernummer = vatId,
        CashRegisterId = Guid.NewGuid(),
        ReceiptNumber = number,
        TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
        PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
    };

    private static RksvSpecialReceiptService CreateRksv(AppDbContext db, ICountryStrategyContext countryContext)
    {
        var seq = new Mock<IReceiptSequenceService>(MockBehavior.Strict);
        var tse = new Mock<ITseService>(MockBehavior.Strict);
        return new RksvSpecialReceiptService(
            db,
            tse.Object,
            seq.Object,
            Mock.Of<IReceiptService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "X",
                TaxNumber = "DE123456789",
                Street = "S",
                ZipCode = "1",
                City = "Y",
            }),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Mock.Of<ILogger<RksvSpecialReceiptService>>(),
            new RksvSpecialReceiptFinanzOnlineSubmissionTracker(db),
            new FinanzOnlineOutboxService(db, Mock.Of<Microsoft.Extensions.Logging.ILogger<FinanzOnlineOutboxService>>()),
            Mock.Of<IReportPdfCaptureService>(),
            Mock.Of<Microsoft.Extensions.Options.IOptionsMonitor<KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineModeOptions>>(
                o => o.CurrentValue == new KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineModeOptions { Mode = "Test" }),
            Mock.Of<Microsoft.Extensions.Options.IOptionsMonitor<KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineCutoverGuardOptions>>(
                o => o.CurrentValue == new KasseAPI_Final.Services.FinanzOnlineIntegration.FinanzOnlineCutoverGuardOptions()),
            countryStrategyContext: countryContext,
            invoiceStrategyResolver: InvoiceResolver);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CountryCallSiteMigration_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static void SeedCountrySettings(
        AppDbContext db,
        string country,
        VatRegime vatRegime,
        string taxNumber)
    {
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Test GmbH",
            CompanyAddress = "S, 1010 Wien",
            CompanyTaxNumber = taxNumber,
            Country = country,
            VatRegime = vatRegime,
            BusinessHours = new Dictionary<string, string>(),
            Currency = country == CountryProfileCodes.Switzerland ? "CHF" : "EUR",
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
