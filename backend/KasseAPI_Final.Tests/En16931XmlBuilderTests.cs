using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class En16931XmlBuilderTests
{
    private static IFeatureFlagService Flags(bool enabled, string featureName)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(featureName, It.IsAny<string?>())).Returns(enabled);
        return mock.Object;
    }

    [Fact]
    public void EuDefaultBinding_SelectsUblBuilder_OtherCountriesThrow()
    {
        var ubl = new En16931UblXmlBuilder();
        var context = new CountryStrategyContext(
            CreateUnusedDb(),
            new CountryProfileRegistry(),
            en16931: ubl);
        var eu = new CountryStrategyBinding(
            new CompanySettings { Country = CountryProfileCodes.EuDefault },
            new CountryProfileRegistry().Get(CountryProfileCodes.EuDefault),
            VatRegime.NON_EU,
            UsedLegacyFallback: false);

        var selected = context.SelectEn16931Builder(eu);

        Assert.Same(ubl, selected);
        Assert.IsType<En16931UblXmlBuilder>(selected);

        var de = eu with
        {
            Profile = new CountryProfileRegistry().Get(CountryProfileCodes.Germany),
        };
        var ex = Assert.Throws<EInvoicingNotSupportedForCountryException>(() => context.SelectEn16931Builder(de));
        Assert.Equal(CountryProfileCodes.Germany, ex.CountryCode);
    }

    private static AppDbContext CreateUnusedDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"eu-builder-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static InvoiceDocumentDto Document() => new() { CountryCode = "EU_DEFAULT" };

    private static InvoiceDocumentDto Fixture(string category, decimal net, decimal tax, decimal vatPercent) => new()
    {
        CountryCode = "EU_DEFAULT",
        InvoiceNumber = "EU-2026-1",
        InvoiceDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        Currency = "EUR",
        SellerName = "Seller GmbH",
        SellerVatId = "ATU12345678",
        SellerStreet = "Hauptstrasse 1",
        SellerCity = "Wien",
        SellerPostalCode = "1010",
        SellerCountry = "AT",
        BuyerName = "Buyer BV",
        BuyerVatId = "DE123456789",
        BuyerStreet = "Berliner Strasse 2",
        BuyerCity = "Berlin",
        BuyerPostalCode = "10115",
        BuyerCountry = "DE",
        PerformanceDescription = "Goods",
        NetAmount = net,
        TaxAmount = tax,
        GrossAmount = net + tax,
        VatCategory = category,
        VatPercent = vatPercent,
        TaxExemptionReason = category == "AE" ? "Reverse charge" : null,
        TaxExemptionReasonCode = category == "AE" ? "VATEX-EU-AE" : null,
    };

    [Fact]
    public async Task BuildXmlAsync_StandardFixture_PassesSchematron()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("S", 100m, 20m, 20m));

        Assert.Contains("urn:cen.eu:en16931:2017", xml, StringComparison.Ordinal);
        Assert.Contains(">380<", xml, StringComparison.Ordinal);
        Assert.Empty(En16931Schematron.Validate(xml));
    }

    [Fact]
    public async Task BuildXmlAsync_ReverseChargeFixture_PassesSchematron()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("AE", 100m, 0m, 0m));

        Assert.Contains("VATEX-EU-AE", xml, StringComparison.Ordinal);
        Assert.Empty(En16931Schematron.Validate(xml));
    }

    [Fact]
    public async Task Schematron_MissingInvoiceNumber_FailsBr02()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("S", 100m, 20m, 20m));
        xml = xml.Replace("<cbc:ID>EU-2026-1</cbc:ID>", "<cbc:ID></cbc:ID>", StringComparison.Ordinal);

        var failures = En16931Schematron.Validate(xml);
        Assert.Contains(failures, f => f.StartsWith("BR-02", StringComparison.Ordinal));
        var result = En16931Schematron.Evaluate(xml);
        Assert.False(result.Passed);
        Assert.Equal("fail", result.Outcome);
        Assert.Contains("BR-02", result.RuleIds);
    }

    [Fact]
    public async Task Schematron_KnownGoodUbl_Passes_KnownBadMissingNumber_Fails()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var good = await builder.BuildXmlAsync(Fixture("S", 100m, 20m, 20m));
        var goodResult = En16931Schematron.Evaluate(good);
        Assert.True(goodResult.Passed);
        Assert.Empty(goodResult.RuleIds);

        var bad = good.Replace("<cbc:ID>EU-2026-1</cbc:ID>", "<cbc:ID></cbc:ID>", StringComparison.Ordinal);
        var badResult = En16931Schematron.Evaluate(bad);
        Assert.False(badResult.Passed);
        Assert.Contains("BR-02", badResult.RuleIds);
        Assert.DoesNotContain("<", string.Join(',', badResult.RuleIds), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAndStore_FlagOn_WritesRuleIds_NotXml()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"eu-validate-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "EU-2026-1",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = InvoiceStatus.Draft,
            CompanyName = "Seller",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            TseSignature = "x",
            KassenId = "KA-01",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = Guid.NewGuid(),
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var sut = new En16931InvoiceValidationService(
            db,
            builder,
            Flags(true, FeatureFlagNames.EInvoicingEn16931),
            audit.Object);

        var result = await sut.ValidateAndStoreAsync(invoice.Id, Fixture("S", 100m, 5m, 20m));

        Assert.NotNull(result);
        Assert.False(result!.Passed);
        Assert.Contains("BR-S-01", result.RuleIds);
        var stored = await db.Invoices.SingleAsync(row => row.Id == invoice.Id);
        Assert.False(stored.EinvoiceValidationPassed);
        Assert.Contains("BR-S-01", stored.EinvoiceValidationRuleIds, StringComparison.Ordinal);
        Assert.DoesNotContain("<cbc:", stored.EinvoiceValidationRuleIds, StringComparison.Ordinal);
        audit.Verify(a => a.LogSystemOperationAsync(
            "EINVOICE_VALIDATED",
            "Invoice",
            "system",
            "SuperAdmin",
            stored.EinvoiceValidationRuleIds,
            It.IsAny<string?>(),
            AuditLogStatus.Failed,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.EinvoiceValidated,
            invoice.Id,
            invoice.TenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndStore_FlagOff_DoesNotRun()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"eu-validate-off-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "EU-2026-1",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = InvoiceStatus.Draft,
            CompanyName = "Seller",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            TseSignature = "x",
            KassenId = "KA-01",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = Guid.NewGuid(),
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>(MockBehavior.Strict);
        var sut = new En16931InvoiceValidationService(
            db,
            new En16931UblXmlBuilder(Flags(false, FeatureFlagNames.EInvoicingEn16931)),
            Flags(false, FeatureFlagNames.EInvoicingEn16931),
            audit.Object);

        var result = await sut.ValidateAndStoreAsync(invoice.Id, Fixture("S", 100m, 20m, 20m));

        Assert.Null(result);
        var stored = await db.Invoices.SingleAsync(row => row.Id == invoice.Id);
        Assert.Null(stored.EinvoiceValidationPassed);
        Assert.Null(stored.EinvoiceValidationRuleIds);
    }

    [Fact]
    public async Task BuildXmlAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new En16931UblXmlBuilder(
            Flags(false, FeatureFlagNames.EInvoicingEn16931));

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(Document()));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
    }

    [Fact]
    public async Task XrechnungBuilder_FlagOn_ThrowsNotImplemented()
    {
        var builder = new NotImplementedXrechnungXmlBuilder(
            Flags(true, FeatureFlagNames.EInvoicingXRechnung));

        var ex = await Assert.ThrowsAsync<EInvoicingNotSupportedForCountryException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Equal(EInvoicingNotSupportedForCountryException.Code, ex.ErrorCode);
        Assert.Equal("DE", ex.CountryCode);
    }

    [Fact]
    public async Task XrechnungBuilder_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new NotImplementedXrechnungXmlBuilder(
            Flags(false, FeatureFlagNames.EInvoicingXRechnung));

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingXRechnung, ex.FeatureName);
    }
}
