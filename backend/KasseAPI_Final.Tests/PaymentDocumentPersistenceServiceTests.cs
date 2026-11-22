using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentDocumentPersistenceServiceTests
{
    [Fact]
    public async Task Persist_StagesPaymentInvoiceAndSoldImei_WithoutSaveOrCommit()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var tx = new Mock<IDbContextTransaction>();
        var imei = new ProductImei { Imei = "123456789012345", Status = ProductImeiStatus.InStock };
        var (request, gates, sale, totals, settlement) = Sale(imei: imei);
        request.CashGiven = 20m;
        var signer = Signer();
        var receipts = ReceiptsThatAttach(ctx);
        var allocated = false;
        var sut = Create(ctx, signer.Object, receipts.Object, (binding, transaction, registerId, registerNumber) =>
        {
            allocated = true;
            Assert.Same(tx.Object, transaction);
            Assert.Equal(request.CashRegisterId, registerId);
            return Task.FromResult("AT-TEST-1");
        });

        var result = await sut.PersistFiscalDocumentsAsync(
            request, "cashier", tx.Object, gates, sale, totals, settlement,
            request.CashRegisterId, "K1", "idem-1", null, null);

        Assert.Null(result.Stop);
        Assert.Equal("AT-TEST-1", result.Value!.Payment.ReceiptNumber);
        Assert.Equal("sig-value", result.Value.Payment.TseSignature);
        Assert.Equal(20m, result.Value.Payment.CashGiven);
        Assert.Equal(10m, result.Value.Payment.ChangeDue);
        Assert.Equal(result.Value.Payment.Id, result.Value.Invoice.SourcePaymentId);
        Assert.Equal("AT-TEST-1", result.Value.Invoice.InvoiceNumber);
        Assert.Equal(ProductImeiStatus.Sold, imei.Status);
        Assert.Equal(result.Value.Payment.Id, imei.SoldPaymentId);
        Assert.Equal(EntityState.Added, ctx.Entry(result.Value.Payment).State);
        Assert.Equal(EntityState.Added, ctx.Entry(result.Value.Invoice).State);
        Assert.True(allocated);
        Assert.Null(ctx.Database.CurrentTransaction);
        Assert.Equal(0, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.Equal(0, await ctx.Invoices.AsNoTracking().CountAsync());
        Assert.Equal(0, await ctx.Receipts.AsNoTracking().CountAsync());
        tx.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        signer.Verify(service => service.SignAsync(
            It.Is<FiscalSignatureContext>(context => context.DbTransaction == tx.Object && context.CashRegisterId == request.CashRegisterId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Persist_RejectsReservedReceipt_WithoutStaging()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            ReceiptNumber = "RESERVED-1",
            IsActive = true,
            CustomerId = Guid.NewGuid(),
            TotalAmount = 1m
        });
        await ctx.SaveChangesAsync();
        var (request, gates, sale, totals, settlement) = Sale();
        request.ReservedReceiptNumber = " RESERVED-1 ";
        var allocateCalled = false;
        var sut = Create(ctx, Signer().Object, new Mock<IReceiptService>().Object, (_, _, _, _) =>
        {
            allocateCalled = true;
            return Task.FromResult("should-not-allocate");
        });

        var result = await sut.PersistFiscalDocumentsAsync(
            request, "cashier", new Mock<IDbContextTransaction>().Object, gates, sale, totals, settlement,
            request.CashRegisterId, "K1", null, null, null);

        Assert.Equal("RECEIPT_NUMBER_CONFLICT", result.Stop?.DiagnosticCode);
        Assert.False(allocateCalled);
        Assert.Equal(1, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<PaymentDetails>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task Persist_TseUnavailable_DoesNotSave()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var signer = new Mock<IFiscalSignatureRouter>();
        signer.Setup(service => service.SignAsync(It.IsAny<FiscalSignatureContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TseUnavailableException("TSE down"));
        var (request, gates, sale, totals, settlement) = Sale();
        var sut = Create(ctx, signer.Object, new Mock<IReceiptService>().Object, (_, _, _, _) => Task.FromResult("AT-TEST-2"));

        var result = await sut.PersistFiscalDocumentsAsync(
            request, "cashier", new Mock<IDbContextTransaction>().Object, gates, sale, totals, settlement,
            request.CashRegisterId, "K1", null, null, null);

        Assert.Equal("TSE_UNAVAILABLE", result.Stop?.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<PaymentDetails>(), entry => entry.State == EntityState.Added);
        Assert.Null(ctx.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Persist_IssuesTicketsAndPlansNothingOnTheLedger_WhenSaleHasNoVoucher()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (request, gates, sale, totals, settlement) = Sale(ticket: true);
        var tickets = new Mock<ITicketRedemptionService>();
        tickets.Setup(service => service.IssueForSaleAsync(
                totals.EffectiveTenantId,
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<(Product Product, int Quantity)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IssuedTicketDto> { new() { Code = "T-1", DisplayCode = "T-1" } });
        var ledger = new Mock<IPaymentVoucherLedgerService>();
        var profiles = new Mock<IVerticalProfileGuard>();
        profiles.Setup(service => service.IsFeatureEnabledAsync("ticketScan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = Create(
            ctx,
            Signer().Object,
            ReceiptsThatAttach(ctx).Object,
            (_, _, _, _) => Task.FromResult("AT-TEST-3"),
            tickets.Object,
            profiles.Object,
            ledger.Object);

        var result = await sut.PersistFiscalDocumentsAsync(
            request, "cashier", new Mock<IDbContextTransaction>().Object, gates, sale, totals, settlement,
            request.CashRegisterId, "K1", null, null, null);

        Assert.Null(result.Stop);
        Assert.Equal("T-1", Assert.Single(result.Value!.IssuedTickets).Code);
        Assert.Equal(0, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        ledger.Verify(service => service.ApplyVoucherRedemptionsInCurrentTransactionAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<PaymentDetails>(),
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<VoucherRedeemLine>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Persist_AppliesVoucherPlanInsideTheOpenTransaction_WithoutSaveOrCommit()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (request, gates, sale, totals, settlement) = Sale();
        var receiptId = Guid.NewGuid();
        settlement = new SaleSettlement
        {
            MethodResolution = settlement.MethodResolution,
            VoucherRedeemLines = [new VoucherRedeemLine { Amount = 10m }],
            TimeSyncWarning = false,
            EffectiveTseRequired = true
        };
        var tx = new Mock<IDbContextTransaction>();
        var ledger = new Mock<IPaymentVoucherLedgerService>();
        ledger.Setup(service => service.ApplyVoucherRedemptionsInCurrentTransactionAsync(
                totals.EffectiveTenantId,
                "cashier",
                It.IsAny<PaymentDetails>(),
                receiptId,
                settlement.VoucherRedeemLines,
                "idem-v",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentResult?)null);
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(service => service.AddReceiptFromPaymentToContextAsync(It.IsAny<PaymentDetails>()))
            .Callback<PaymentDetails>(payment => ctx.Receipts.Add(new Receipt
            {
                ReceiptId = receiptId,
                PaymentId = payment.Id,
                ReceiptNumber = payment.ReceiptNumber,
                TenantId = totals.EffectiveTenantId,
                CashRegisterId = request.CashRegisterId
            }))
            .Returns(Task.CompletedTask);
        var sut = Create(
            ctx,
            Signer().Object,
            receipts.Object,
            (_, _, _, _) => Task.FromResult("AT-TEST-4"),
            ledger: ledger.Object);

        var result = await sut.PersistFiscalDocumentsAsync(
            request, "cashier", tx.Object, gates, sale, totals, settlement,
            request.CashRegisterId, "K1", "idem-v", null, null);

        Assert.Null(result.Stop);
        Assert.Equal(0, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.Equal(0, await ctx.Receipts.AsNoTracking().CountAsync());
        Assert.Null(ctx.Database.CurrentTransaction);
        tx.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        ledger.VerifyAll();
    }

    [Fact]
    public async Task ApplyBeforeSign_LeavesTheCart_WhenNoExtensionAdjustsIt()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx, Signer().Object, new Mock<IReceiptService>().Object, (_, _, _, _) => Task.FromResult("unused"));
        var line = CartMoneyHelper.ComputeLine(10m, 1, 20m);
        var tax = Tax(line);
        var payment = new PaymentDetails { TotalAmount = 10m, TaxAmount = line.LineTax, ReceiptNumber = "R-1" };
        var items = new List<PaymentItem>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 1, TotalPrice = 10m, TaxType = line.TaxType }
        };

        var (returned, details) = await sut.ApplyBeforeSignCartAsync(
            Guid.NewGuid(),
            payment,
            items,
            tax,
            new Dictionary<string, decimal>(tax.TaxDetails),
            Mock.Of<ITaxStrategy>(),
            AustriaBinding(),
            new CountryTaxTypeRegistry(),
            null,
            new Dictionary<Guid, bool>());

        Assert.Same(tax, returned);
        Assert.Equal(10m, payment.TotalAmount);
        Assert.Empty(ctx.ChangeTracker.Entries());
        Assert.Equal(line.LineTax, details[line.TaxType.ToString()]);
    }

    private static PaymentDocumentPersistenceService Create(
        AppDbContext ctx,
        IFiscalSignatureRouter signer,
        IReceiptService receipts,
        CountryBelegAllocator allocate,
        ITicketRedemptionService? tickets = null,
        IVerticalProfileGuard? profiles = null,
        IPaymentVoucherLedgerService? ledger = null) =>
        new(
            ctx,
            NullLogger<PaymentService>.Instance,
            new Mock<IAuditLogService>().Object,
            signer,
            receipts,
            ledger ?? new Mock<IPaymentVoucherLedgerService>().Object,
            allocate,
            tickets,
            extensionRegistry: null,
            profiles);

    private static Mock<IFiscalSignatureRouter> Signer()
    {
        var signer = new Mock<IFiscalSignatureRouter>();
        signer.Setup(service => service.SignAsync(It.IsAny<FiscalSignatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalSignatureResult("AT_FISKALY", "sig-value", null, 1.67m, PrevSignatureValue: "prev", CertificateThumbprint: "thumb"));
        return signer;
    }

    private static Mock<IReceiptService> ReceiptsThatAttach(AppDbContext ctx)
    {
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(service => service.AddReceiptFromPaymentToContextAsync(It.IsAny<PaymentDetails>()))
            .Callback<PaymentDetails>(payment => ctx.Receipts.Add(new Receipt
            {
                PaymentId = payment.Id,
                ReceiptNumber = payment.ReceiptNumber ?? "",
                TenantId = Guid.NewGuid(),
                CashRegisterId = payment.CashRegisterId
            }))
            .Returns(Task.CompletedTask);
        return receipts;
    }

    private static (CreatePaymentRequest Request, PaymentPreconditions Gates, PreparedFiscalSale Sale, SaleTotals Totals, SaleSettlement Settlement) Sale(
        ProductImei? imei = null,
        bool ticket = false)
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, Guid.NewGuid());
        var line = CartMoneyHelper.ComputeLine(10m, 1, 20m);
        var item = new PaymentItem
        {
            ProductId = productId,
            ProductName = "Coffee",
            Quantity = 1,
            UnitPrice = 10m,
            TotalPrice = 10m,
            TaxType = line.TaxType,
            TaxRate = line.TaxRate,
            TaxAmount = line.LineTax,
            LineNet = line.LineNet
        };
        var product = new Product { Id = productId, Name = "Coffee", IsTicket = ticket };
        var totals = new SaleTotals
        {
            EffectiveTenantId = tenantId,
            PaymentItems = [item],
            TotalAmount = 10m,
            TotalTaxAmount = line.LineTax,
            TaxDetails = new Dictionary<string, decimal> { [line.TaxType.ToString()] = line.LineTax },
            TaxResult = Tax(line),
            PricedLines = [(new PaymentItemRequest { ProductId = productId, Quantity = 1 }, product, 10m)],
            PendingImeiRows = imei is null ? [] : [imei],
            PaymentStockLinesSkipped = 0,
            RateCatalog = new CountryTaxTypeRegistry()
        };
        var sale = new PreparedFiscalSale
        {
            Customer = new Customer { Id = customerId, Name = "Gast" },
            CompanyProfile = new CompanyProfileOptions(),
            CountryBinding = AustriaBinding(tenantId),
            TaxStrategy = Mock.Of<ITaxStrategy>(),
            EffectiveSteuernummer = "ATU12345678",
            Principal = new ClaimsPrincipal(),
            VoucherLikelyFromClientMethod = false,
            HasVoucherPayload = false,
            EffectiveTseRequired = true
        };
        var settlement = new SaleSettlement
        {
            MethodResolution = new PaymentMethodResolutionResult(true, "0", null, false),
            TimeSyncWarning = false,
            EffectiveTseRequired = true
        };
        return (request, new PaymentPreconditions { TenantIdForFlags = tenantId }, sale, totals, settlement);
    }

    private static CountryStrategyBinding AustriaBinding(Guid? tenantId = null) =>
        new(
            new CompanySettings { TenantId = tenantId ?? Guid.NewGuid() },
            new CountryProfileRegistry().Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD,
            false);

    private static TaxCalculationResult Tax(CartMoneyHelper.LineAmounts line) =>
        new()
        {
            Lines = [line],
            TaxSummary = [],
            Totals = new CartMoneyHelper.ReceiptTotals(line.LineNet, line.LineTax, line.LineGross),
            TaxDetails = new Dictionary<string, decimal> { [line.TaxType.ToString()] = line.LineTax }
        };
}
