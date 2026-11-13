using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SwitzerlandInvoiceStrategyTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static CompanySettings ChCompany() => new()
    {
        Country = CountryProfileCodes.Switzerland,
        VatRegime = VatRegime.CH_MWST_STANDARD,
        CompanyName = "CH GmbH",
        CompanyAddress = "Zürich",
        CompanyTaxNumber = "CHE-123.456.789 MWST",
        BankAccountNumber = "CH9300762011623852957",
        Currency = "CHF",
    };

    private static PaymentDetails Payment() => new()
    {
        ReceiptNumber = "RE-CH-1",
        TotalAmount = 108.1m,
        TaxAmount = 8.1m,
        Notes = "Beratung",
        CustomerName = "Gast",
        CashierId = "c1",
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        CashRegisterId = Guid.NewGuid(),
    };

    [Fact]
    public void GetMandatoryDisclosures_ContainsMwstFields()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(true));
        var disclosures = strategy.GetMandatoryDisclosures(ChCompany(), null);
        var keys = disclosures.Select(d => d.Key).ToArray();

        Assert.Contains("seller.vatId", keys);
        Assert.Contains("invoice.number", keys);
        Assert.Contains("invoice.date", keys);
        Assert.Contains("buyer.name", keys);
        Assert.Contains("buyer.address", keys);
        Assert.Contains("invoice.net", keys);
        Assert.Contains("invoice.tax", keys);
        Assert.Contains("invoice.gross", keys);
        Assert.Contains("invoice.mwstBreakdown", keys);
        Assert.Contains("invoice.kleinunternehmer", keys);
        Assert.All(disclosures, d => Assert.Equal("MWSTG", d.LegalBasis));
        Assert.Equal(nameof(CompanySettings.VatId), disclosures.Single(d => d.Key == "seller.vatId").SourceField);
        Assert.Equal(nameof(PaymentDetails.ReceiptNumber), disclosures.Single(d => d.Key == "invoice.number").SourceField);
        Assert.Equal(nameof(PaymentDetails.CreatedAt), disclosures.Single(d => d.Key == "invoice.date").SourceField);
    }

    [Fact]
    public async Task BuildInvoiceDocumentAsync_ReturnsStructuredShape_AndKeepsReceipt()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(true));
        var doc = await strategy.BuildInvoiceDocumentAsync(Payment(), ChCompany(), customer: null);

        Assert.Equal(CountryProfileCodes.Switzerland, doc.CountryCode);
        Assert.Equal("RE-CH-1", doc.Receipt.ReceiptNumber);
        Assert.NotNull(doc.Structured);
        Assert.Equal("CHE-123.456.789 MWST", doc.Structured!.SellerVatId);
        Assert.Equal("CHE-123.456.789 MWST", doc.Structured.SellerTaxNumber);
        Assert.Equal("RE-CH-1", doc.Structured.InvoiceNumber);
        Assert.Equal(100m, doc.Structured.NetAmount);
        Assert.Equal(8.1m, doc.Structured.TaxAmount);
        Assert.Equal(108.1m, doc.Structured.GrossAmount);
        Assert.Equal("CHF", doc.Structured.Currency);
        Assert.Equal("Beratung", doc.Structured.PerformanceDescription);
        Assert.NotNull(doc.QrRechnung);
        Assert.Equal(QrRechnungReferenceType.Non, doc.QrRechnung!.ReferenceType);
        Assert.StartsWith("CH", doc.QrRechnung.Iban, StringComparison.Ordinal);
        Assert.Contains("CH9300762011623852957", doc.QrRechnung.SwissQrText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMandatoryDisclosures_FlagOff_ThrowsFeatureDisabled()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(false));
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            strategy.GetMandatoryDisclosures(ChCompany(), null));

        Assert.Equal(FeatureFlagNames.FiscalMwstCh, ex.FeatureName);
    }

    [Fact]
    public async Task AllocateReceiptNumberAsync_WithoutSequenceService_Throws()
    {
        var strategy = new SwitzerlandInvoiceStrategy();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
            {
                CashRegisterId = Guid.NewGuid(),
            }));

        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllocateReceiptNumberAsync_UsesChSequence_NotAustrianReservation()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Bern", Slug = "bern", IsActive = true });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "2",
            IsActive = true,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var next = 0;
        var sequences = new Mock<IChReceiptSequenceService>();
        sequences
            .Setup(s => s.AllocateNextAsync(tenantId, registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++next);

        var strategy = new SwitzerlandInvoiceStrategy(Flags(true), sequences: sequences.Object, db: db);
        var first = await strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
        {
            CashRegisterId = registerId,
        });
        var second = await strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
        {
            CashRegisterId = registerId,
        });

        Assert.Equal("CH-bern-2-1", first);
        Assert.Equal("CH-bern-2-2", second);
        sequences.Verify(s => s.AllocateNextAsync(tenantId, registerId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BuildInvoiceDocumentAsync_AuditsPayloadHash_WithoutIban()
    {
        const string iban = "CH9300762011623852957";
        var tenantId = Guid.NewGuid();
        var payment = Payment();
        var company = ChCompany();
        company.TenantId = tenantId;
        company.BankAccountNumber = iban;

        var calls = new List<AuditCall>();
        var audit = AuditCapturing(calls);
        object? activityMetadata = null;
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.QrRechnungPayloadBuilt,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ActivityEventType, object?, string?, string?, CancellationToken>((_, _, metadata, _, _, _) =>
                activityMetadata = metadata)
            .Returns(Task.CompletedTask);

        var builder = new QrRechnungBuilder(featureFlags: null, audit: audit.Object, activity: activity.Object);
        var strategy = new SwitzerlandInvoiceStrategy(Flags(true), builder);
        await strategy.BuildInvoiceDocumentAsync(payment, company, customer: null);

        var call = Assert.Single(calls);
        Assert.Equal(AuditEventType.QrRechnungPayloadBuilt, call.Type);
        Assert.Equal(tenantId, call.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(call.CorrelationId));
        var json = JsonSerializer.Serialize(call.NewValues);
        Assert.DoesNotContain(iban, json, StringComparison.Ordinal);
        Assert.Contains(payment.Id.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("payloadHash", json, StringComparison.Ordinal);
        var activityJson = JsonSerializer.Serialize(activityMetadata);
        Assert.DoesNotContain(iban, activityJson, StringComparison.Ordinal);
    }

    private static Mock<IAuditLogService> AuditCapturing(List<AuditCall> calls)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback(new InvocationAction(invocation =>
            {
                calls.Add(new AuditCall(
                    invocation.Arguments[12] as AuditEventType?,
                    invocation.Arguments[10] as string,
                    invocation.Arguments[14] as Guid?,
                    invocation.Arguments[16]));
            }))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private sealed record AuditCall(AuditEventType? Type, string? CorrelationId, Guid? TenantId, object? NewValues);

    [Fact]
    public async Task AllocateReceiptNumberAsync_SkipsAReceiptNumberAlreadyOnPaymentDetails()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Bern", Slug = "bern", IsActive = true });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "2",
            IsActive = true,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = registerId,
            ReceiptNumber = "CH-bern-2-1",
            IsActive = true,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            CashierId = "c1",
            TotalAmount = 1m,
            Steuernummer = "ATU12345678",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var next = 0;
        var sequences = new Mock<IChReceiptSequenceService>();
        sequences
            .Setup(s => s.AllocateNextAsync(tenantId, registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++next);

        var strategy = new SwitzerlandInvoiceStrategy(Flags(true), sequences: sequences.Object, db: db);
        var number = await strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
        {
            CashRegisterId = registerId,
        });

        Assert.Equal("CH-bern-2-2", number);
    }

    [Fact]
    public async Task AustrianInvoice_StillAllocatesThroughSequenceReservationService()
    {
        const string reserved = "AT-KASSE-BASELINE-01-20260916-7";
        var cashRegisterId = Guid.NewGuid();
        var sequences = new Mock<ISequenceReservationService>();
        sequences
            .Setup(s => s.ReserveNextReceiptNumberAsync(cashRegisterId, It.IsAny<CancellationToken>(), 3))
            .ReturnsAsync(reserved);

        var strategy = new AustriaInvoiceStrategy(sequences.Object, Mock.Of<IReceiptService>());
        var number = await strategy.AllocateReceiptNumberAsync(
            new ReceiptNumberAllocationContext { CashRegisterId = cashRegisterId });

        Assert.Equal(reserved, number);
        sequences.Verify(
            s => s.ReserveNextReceiptNumberAsync(cashRegisterId, It.IsAny<CancellationToken>(), 3),
            Times.Once);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ChReceipt_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }
}
