using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ChMwstEffectiveRatesTests
{
    private const string OldRatesJson = """{"STANDARD":7.7,"REDUCED_1":2.5,"LODGING":3.7}""";
    private const string NewRatesJson = """{"STANDARD":8.1,"REDUCED_1":2.6,"LODGING":3.8}""";

    [Fact]
    public async Task OldChRates_HistoricalInvoiceUnchanged_AfterRateChange()
    {
        Assert.Null(typeof(CompanySettings).GetProperty("CountryCode"));

        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var now = new DateTime(2023, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "CH Alt",
            Slug = "ch-alt",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = now,
        });
        db.CompanySettings.Add(Company(tenantId));
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "KASSE-CH",
            Location = "Zürich",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            CreatedAt = now,
            IsActive = true,
        });
        db.TenantSettings.Add(new TenantSetting
        {
            TenantId = tenantId,
            Key = ChMwstRateSettings.Key,
            Value = OldRatesJson,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var seeds = new CountryTaxTypeRegistry();
        var rates = new ChMwstEffectiveRates(db, seeds);
        var strategy = new SwitzerlandTaxStrategy(
            seeds,
            new VatIdValidator(new DisabledViesClient()),
            Flags(enabled: true),
            rates);
        var context = new TaxCalculationContext
        {
            CountryProfile = new CountryProfileRegistry().Get(CountryProfileCodes.Switzerland),
            VatRegime = VatRegime.CH_MWST_STANDARD,
            TaxExempt = false,
            TenantId = tenantId,
        };

        var oldLine = CartMoneyHelper.ComputeLine(107.7m, 1, 7.7m);
        var issued = strategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(107.7m, 1, 7.7m)],
            context);
        Assert.Equal(oldLine, issued.Lines[0]);
        Assert.Equal(oldLine.LineTax, issued.TaxDetails[CountryTaxTypeCodes.Standard]);

        var reduced = strategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(102.5m, 1, 2.5m)],
            context);
        var lodging = strategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(103.7m, 1, 3.7m)],
            context);
        Assert.Equal(CartMoneyHelper.ComputeLine(102.5m, 1, 2.5m).LineTax, reduced.Totals.TotalVat);
        Assert.Equal(CartMoneyHelper.ComputeLine(103.7m, 1, 3.7m).LineTax, lodging.Totals.TotalVat);

        var payment = new PaymentDetails
        {
            ReceiptNumber = "RE-CH-OLD",
            TotalAmount = oldLine.LineGross,
            TaxAmount = oldLine.LineTax,
            Notes = "Beratung",
            CustomerName = "Gast",
            CashierId = "c1",
            PaymentMethodRaw = "0",
            Steuernummer = "CHE-123.456.789 MWST",
            CashRegisterId = registerId,
            CountryCodeAtIssue = CountryProfileCodes.Switzerland,
            VatRegimeAtIssue = VatRegime.CH_MWST_STANDARD,
            CreatedAt = now,
        };
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "RE-CH-OLD",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Paid,
            Subtotal = oldLine.LineNet,
            TaxAmount = oldLine.LineTax,
            TotalAmount = oldLine.LineGross,
            PaidAmount = oldLine.LineGross,
            RemainingAmount = 0,
            CompanyName = "CH GmbH",
            CompanyTaxNumber = "CHE-123.456.789 MWST",
            CompanyAddress = "Zürich",
            TseSignature = "sig",
            KassenId = "KASSE-CH",
            TseTimestamp = now,
            CashRegisterId = registerId,
            CountryCodeAtIssue = CountryProfileCodes.Switzerland,
            VatRegimeAtIssue = VatRegime.CH_MWST_STANDARD,
            TaxDetails = JsonDocument.Parse("""{"STANDARD":7.7}"""),
            CreatedAt = now,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var setting = await db.TenantSettings.SingleAsync(s => s.TenantId == tenantId && s.Key == ChMwstRateSettings.Key);
        setting.Value = NewRatesJson;
        setting.UpdatedAtUtc = now.AddYears(1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var preserved = await db.Invoices.SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(CountryProfileCodes.Switzerland, preserved.CountryCodeAtIssue);
        Assert.Equal(VatRegime.CH_MWST_STANDARD, preserved.VatRegimeAtIssue);
        Assert.Equal(oldLine.LineTax, preserved.TaxAmount);
        Assert.Equal(oldLine.LineNet, preserved.Subtotal);
        Assert.Equal(oldLine.LineGross, preserved.TotalAmount);
        Assert.Equal(1, await db.Invoices.CountAsync(i => i.TenantId == tenantId));

        var company = await db.CompanySettings.SingleAsync(s => s.TenantId == tenantId);
        Assert.Equal(CountryProfileCodes.Switzerland, company.Country);

        var after = strategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
            context);
        var newLine = CartMoneyHelper.ComputeLine(108.1m, 1, 8.1m);
        Assert.Equal(newLine.LineTax, after.Totals.TotalVat);
        Assert.NotEqual(oldLine.LineTax, after.Totals.TotalVat);
        Assert.Throws<ArgumentException>(() => strategy.CalculateTax(
            [TaxLineItemInput.FromVatPercent(107.7m, 1, 7.7m)],
            context));

        var document = await new SwitzerlandInvoiceStrategy(Flags(enabled: true))
            .BuildInvoiceDocumentAsync(payment, Company(tenantId), customer: null);
        Assert.Equal(CountryProfileCodes.Switzerland, document.CountryCode);
        Assert.Equal(oldLine.LineTax, document.Structured!.TaxAmount);
        Assert.Equal(oldLine.LineGross, document.Structured.GrossAmount);

        var atStandard = seeds.Get(CountryProfileCodes.Austria)
            .Single(t => t.Code == CountryTaxTypeCodes.Standard)
            .Rate;
        Assert.Equal(20m, atStandard);
        Assert.Equal(
            atStandard,
            rates.ForCalculation(seeds, tenantId).Get(CountryProfileCodes.Austria)
                .Single(t => t.Code == CountryTaxTypeCodes.Standard)
                .Rate);

        var controller = new AdminMwstRatesController(db, rates);
        var viewed = await controller.GetRates(tenantId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(viewed.Result);
        var dto = Assert.IsType<ChMwstEffectiveRatesDto>(ok.Value);
        Assert.True(dto.Applies);
        Assert.Equal(ChMwstRateSettings.SourceTenantOverride, dto.Source);
        Assert.Equal(CountryProfileCodes.Switzerland, dto.Country);
        Assert.Equal(8.1m, dto.Rates.Single(r => r.Code == CountryTaxTypeCodes.Standard).Rate);
        Assert.Equal(2.6m, dto.Rates.Single(r => r.Code == CountryTaxTypeCodes.Reduced1).Rate);
        Assert.Equal(3.8m, dto.Rates.Single(r => r.Code == CountryTaxTypeCodes.Lodging).Rate);

        var still = await db.Invoices.SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(CountryProfileCodes.Switzerland, still.CountryCodeAtIssue);
        Assert.Equal(oldLine.LineTax, still.TaxAmount);
    }

    [Fact]
    public async Task GetRates_MissingTenant_Returns404_AtTenantIgnoresChOverride()
    {
        var atTenant = Guid.NewGuid();
        await using var db = CreateDb(atTenant);
        db.Tenants.Add(new Tenant
        {
            Id = atTenant,
            Name = "AT",
            Slug = "at-rates",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var settings = Company(atTenant);
        settings.Country = CountryProfileCodes.Austria;
        settings.Currency = "EUR";
        settings.VatRegime = VatRegime.AT_RKSV_STANDARD;
        db.CompanySettings.Add(settings);
        db.TenantSettings.Add(new TenantSetting
        {
            TenantId = atTenant,
            Key = ChMwstRateSettings.Key,
            Value = OldRatesJson,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeds = new CountryTaxTypeRegistry();
        var rates = new ChMwstEffectiveRates(db, seeds);
        var controller = new AdminMwstRatesController(db, rates);

        var missing = await controller.GetRates(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(missing.Result);

        var viewed = await controller.GetRates(atTenant, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(viewed.Result);
        var dto = Assert.IsType<ChMwstEffectiveRatesDto>(ok.Value);
        Assert.False(dto.Applies);
        Assert.Equal(ChMwstRateSettings.SourceSeed, dto.Source);
        Assert.Equal(CountryProfileCodes.Austria, dto.Country);
        Assert.Equal(8.1m, dto.Rates.Single(r => r.Code == CountryTaxTypeCodes.Standard).Rate);
        Assert.Equal(
            20m,
            rates.ForCalculation(seeds, atTenant).Get(CountryProfileCodes.Austria)
                .Single(t => t.Code == CountryTaxTypeCodes.Standard)
                .Rate);
    }

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ChMwstRates_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static CompanySettings Company(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CompanyName = "CH GmbH",
        CompanyAddress = "Zürich",
        CompanyTaxNumber = "CHE-123.456.789 MWST",
        BankAccountNumber = "CH9300762011623852957",
        Country = CountryProfileCodes.Switzerland,
        VatRegime = VatRegime.CH_MWST_STANDARD,
        Currency = "CHF",
        Language = "de",
        TimeZone = "Europe/Zurich",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm",
        TaxCalculationMethod = "inclusive",
        InvoiceNumbering = "INV",
        ReceiptNumbering = "R",
        DefaultPaymentMethod = "Cash",
        BusinessHours = new Dictionary<string, string>(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };
}
