using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PaymentService coverage for Fiskaly signing, storno/refund, offline queue/replay,
/// TSE unavailability, and query/error branches that were previously untested.
/// </summary>
public sealed class PaymentServiceFiskalyCoverageTests
{
    private const string FiskalyMachineCode =
        "_R1-AT1_KASSE-01_1_2026-08-16T12:00:00_10,00_0,00_0,00_0,00_0,00_abc_123_0_sig";

    private const string CashierId = "cashier1";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayFiskaly_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static Mock<ITseService> CreateFiskalyTseMock(string machineCode = FiskalyMachineCode)
    {
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult(
                machineCode,
                "prev-fiskaly",
                CertificateThumbprint: "fiskaly-thumb",
                FiskalyQrCodeData: machineCode,
                SigningProvider: "Fiskaly"));
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "fiskaly-cert" });
        tse.Setup(x => x.GetDeviceStatusAsync())
            .ReturnsAsync(new TseStatus { IsConnected = true, IsReady = true, Status = "Connected" });
        return tse;
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static PaymentService CreatePaymentService(
        AppDbContext context,
        Mock<ITseService> tseMock,
        TseOptions? tseOptions = null,
        ITseHealthMonitor? tseHealthMonitor = null,
        IHostEnvironment? hostEnvironment = null,
        Mock<IReceiptSequenceService>? receiptSeqMock = null,
        bool demoUser = false,
        ITenantLimitGuard? tenantLimitGuard = null)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true, Status = "Submitted" });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser
            {
                Id = CashierId,
                UserName = CashierId,
                Role = "Cashier",
                IsDemo = demoUser
            });

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test GmbH",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        receiptSeqMock ??= new Mock<IReceiptSequenceService>();
        var seq = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                $"AT-{reg}-{d:yyyyMMdd}-{++seq}");

        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment);

        var cashRegResolver = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, CashierId),
            new Claim(ClaimTypes.Role, "Cashier"),
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(http);

        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            new NoOpProductModifierValidationService(),
            receiptSeqMock.Object,
            receiptService,
            CreateAuditMock().Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(tseOptions ?? new TseOptions { TseMode = "Demo" }),
            Options.Create(new InventoryOptions { EnforceStockOnSales = false }),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            accessor.Object,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver,
            tseHealthMonitor: tseHealthMonitor,
            hostEnvironment: hostEnvironment,
            tenantLimitGuard: tenantLimitGuard);
    }

    private static async Task<(Guid CustomerId, Guid ProductId, Guid CashRegisterId)> SeedCatalogAsync(
        AppDbContext context,
        decimal unitPrice = 10m)
    {
        TenantTestDoubles.EnsurePlatformTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = SystemTenantIds.Platform,
            Name = "Schnitzel",
            Price = unitPrice,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        context.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Gast",
            Email = "g@t.com",
            Phone = "1",
            IsActive = true
        });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (customerId, productId, cashRegisterId);
    }

    private static CreatePaymentRequest CashSaleRequest(Guid customerId, Guid productId, Guid registerId, decimal total = 10m) =>
        new()
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = total,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

    private static ITseHealthMonitor OfflineHealthMonitor()
    {
        var mock = new Mock<ITseHealthMonitor>();
        mock.SetupGet(m => m.Snapshot).Returns(new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Offline,
            LastCheckUtc = DateTime.UtcNow,
            ConsecutiveFailures = 5,
            LastErrorMessageSafe = "Fiskaly unreachable"
        });
        return mock.Object;
    }

    [Fact]
    public async Task CreatePayment_WithFiskaly_SignsAndSetsFiskalyProvider()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(ctx, tse);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(FiskalyMachineCode, result.TseSignature);
        Assert.Equal(FiskalyMachineCode, result.QrPayload);
        Assert.Equal("Fiskaly", result.TseProvider);
        Assert.False(result.IsDemoFiscal);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                registerId,
                It.IsAny<string>(),
                10m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenTseUnavailable_ReturnsTseUnavailableDiagnostic()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new TseUnavailableException("TSE is not available (Fiskaly not ready)"));
        var sut = CreatePaymentService(ctx, tse);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.False(result.Success);
        Assert.Equal("TSE_UNAVAILABLE", result.DiagnosticCode);
        Assert.Equal("TSE is not available", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenTseSignatureFails_ReturnsFailureWithoutPersisting()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new InvalidOperationException("Fiskaly HTTP 503"));
        var sut = CreatePaymentService(ctx, tse);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.False(result.Success);
        Assert.Equal("Failed to generate TSE signature", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenTseHealthOffline_QueuesNonFiscalIntent()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(
            ctx,
            tse,
            tseOptions: new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.NonFiscalOfflineQueued);
        Assert.Equal("NON_FISCAL_QUEUED", result.DiagnosticCode);
        Assert.NotNull(result.OfflineTransactionId);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<IDbContextTransaction?>()),
            Times.Never);
        Assert.Equal(1, await ctx.OfflineTransactions.CountAsync(x => x.Status == OfflineTransactionStatus.NonFiscalPending));
    }

    [Fact]
    public async Task CreatePayment_WhenTseHealthOfflineAndOfflineDisabled_ReturnsTseHealthOffline()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(
            ctx,
            CreateFiskalyTseMock(),
            tseOptions: new TseOptions { TseMode = "Device", OfflineModeEnabled = false },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.False(result.Success);
        Assert.Equal("TSE_HEALTH_OFFLINE", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenTseHealthOffline_VoucherBlocked()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(
            ctx,
            CreateFiskalyTseMock(),
            tseOptions: new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment);

        var request = CashSaleRequest(customerId, productId, registerId);
        request.Payment = new PaymentMethodRequest
        {
            Method = "voucher",
            TseRequired = true,
            VoucherCode = "TEST-CODE-1"
        };

        var result = await sut.CreatePaymentAsync(request, CashierId);

        Assert.False(result.Success);
        Assert.Equal("TSE_OFFLINE_VOUCHER_BLOCKED", result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
    }

    [Fact]
    public async Task CreatePayment_WhenTseHealthOffline_UnsupportedMethodBlocked()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(
            ctx,
            CreateFiskalyTseMock(),
            tseOptions: new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment);

        var request = CashSaleRequest(customerId, productId, registerId);
        request.Payment = new PaymentMethodRequest { Method = "invoice", TseRequired = true };

        var result = await sut.CreatePaymentAsync(request, CashierId);

        Assert.False(result.Success);
        Assert.Equal("TSE_OFFLINE_METHOD_UNSUPPORTED", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenOfflineQueueFull_ReturnsOfflineQueueFull()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var guard = new Mock<ITenantLimitGuard>();
        guard
            .Setup(g => g.EnsureCanQueueOfflineTransactionAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimitExceededException(
                TenantLimitKeys.MaxOfflineTransactions,
                1,
                1,
                "Offline queue limit of 1 reached"));

        var sut = CreatePaymentService(
            ctx,
            CreateFiskalyTseMock(),
            tseOptions: new TseOptions
            {
                TseMode = "Device",
                OfflineModeEnabled = true
            },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment,
            tenantLimitGuard: guard.Object);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.False(result.Success);
        Assert.Equal(LimitExceededException.ErrorCodeValue, result.DiagnosticCode);
        Assert.NotNull(result.LimitError);
    }

    [Fact]
    public async Task CreatePayment_WhenTenantOfflineQueueLimitExceeded_ReturnsOfflineQueueFull()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);

        var guard = new Mock<ITenantLimitGuard>();
        guard
            .Setup(g => g.EnsureCanQueueOfflineTransactionAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimitExceededException(
                TenantLimitKeys.MaxOfflineTransactions,
                50,
                50,
                "Offline queue limit of 50 reached"));

        var sut = CreatePaymentService(
            ctx,
            CreateFiskalyTseMock(),
            tseOptions: new TseOptions
            {
                TseMode = "Device",
                OfflineModeEnabled = true
            },
            tseHealthMonitor: OfflineHealthMonitor(),
            hostEnvironment: TenantTestDoubles.ProductionHostEnvironment,
            tenantLimitGuard: guard.Object);

        var result = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);

        Assert.False(result.Success);
        Assert.Equal(LimitExceededException.ErrorCodeValue, result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
        Assert.Contains("Offline queue limit of 50", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Storno_WithFiskaly_SignsCorrectly()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(ctx, tse);

        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);
        var saleId = sale.Payment!.Id;

        var storno = await sut.CancelPaymentAsync(
            saleId,
            "Kunde hat storniert",
            CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        Assert.NotNull(storno.Payment);
        Assert.True(storno.Payment!.IsStorno);
        Assert.Equal(-10m, storno.Payment.TotalAmount);
        Assert.Equal(FiskalyMachineCode, storno.Payment.TseSignature);
        Assert.Equal(saleId, storno.Payment.OriginalPaymentId);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                registerId,
                It.IsAny<string>(),
                -10m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task Storno_WhenTseUnavailable_DoesNotPersistReversal()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(ctx, tse);
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new TseUnavailableException("Fiskaly down"));

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.False(storno.Success);
        Assert.Equal("Failed to generate TSE signature for reversal", storno.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsStorno));
    }

    [Fact]
    public async Task Storno_SecondCall_ReturnsAlreadyCancelled()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var first = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);
        Assert.True(first.Success, first.Message);

        var second = await sut.CancelPaymentAsync(
            sale.Payment.Id,
            "Kunde hat storniert",
            CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.False(second.Success);
        Assert.Equal("ALREADY_CANCELLED", second.DiagnosticCode);
        Assert.True(second.IsDeterministicFailure);
    }

    [Fact]
    public async Task CancelPayment_WhenReasonTooShort_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());

        var result = await sut.CancelPaymentAsync(Guid.NewGuid(), "no", CashierId);

        Assert.False(result.Success);
        Assert.Equal("CANCELLATION_REASON_REQUIRED", result.DiagnosticCode);
    }

    [Fact]
    public async Task CancelPayment_WhenDemoUser_IsRejected()
    {
        await using var ctx = CreateContext();
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock(), demoUser: true);

        var result = await sut.CancelPaymentAsync(Guid.NewGuid(), "Kunde hat storniert", CashierId);

        Assert.False(result.Success);
        Assert.Contains("Demo users cannot cancel", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refund_WithFiskaly_SignsCorrectly()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(ctx, tse);
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            10m,
            "Kunde reklamiert Ware",
            CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.True(refund.Success, refund.Message + ": " + string.Join("; ", refund.Errors));
        Assert.NotNull(refund.Payment);
        Assert.True(refund.Payment!.IsRefund);
        Assert.Equal(-10m, refund.Payment.TotalAmount);
        Assert.Equal(FiskalyMachineCode, refund.Payment.TseSignature);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                registerId,
                It.IsAny<string>(),
                -10m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task Refund_WhenAmountExceedsRemaining_ReturnsDeterministicFailure()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            99m,
            "Kunde reklamiert Ware",
            CashierId);

        Assert.False(refund.Success);
        Assert.Equal("REFUND_EXCEEDS_REMAINING", refund.DiagnosticCode);
        Assert.True(refund.IsDeterministicFailure);
    }

    [Fact]
    public async Task Refund_WhenTseUnavailable_DoesNotPersistRefund()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var sut = CreatePaymentService(ctx, tse);
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new TseUnavailableException("Fiskaly down"));

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            10m,
            "Kunde reklamiert Ware",
            CashierId);

        Assert.False(refund.Success);
        Assert.Equal("Failed to generate TSE signature for refund", refund.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsRefund));
    }

    [Fact]
    public async Task OfflineReplay_WithFiskaly_SignsWhenAvailable()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        var paymentService = CreatePaymentService(ctx, tse);
        var offlineService = new OfflineTransactionService(
            ctx,
            paymentService,
            CreateAuditMock().Object,
            Mock.Of<ILogger<OfflineTransactionService>>(),
            new EphemeralDataProtectionProvider());

        var paymentRequest = CashSaleRequest(customerId, productId, registerId);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(paymentRequest));
        var payload = doc.RootElement.Clone();

        var replay = await offlineService.ReplayOfflineTransactionsAsync(
            new ReplayOfflineTransactionsRequest
            {
                Transactions =
                [
                    new ReplayOfflineTransactionItem
                    {
                        OfflineTransactionId = Guid.NewGuid(),
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                        CashRegisterId = registerId,
                        Payload = payload
                    }
                ]
            },
            CashierId,
            "Cashier");

        var item = Assert.Single(replay.Items);
        Assert.Equal("Synced", item.Status);
        var payment = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        Assert.Equal(FiskalyMachineCode, payment.TseSignature);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                registerId,
                It.IsAny<string>(),
                10m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetQrPayloadForPayment_WithFiskalySignature_ReturnsMachineCode()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var qr = await sut.GetQrPayloadForPaymentAsync(sale.Payment!.Id);

        Assert.NotNull(qr);
        Assert.Equal(FiskalyMachineCode, qr.Value.QrPayload);
    }

    [Fact]
    public async Task GenerateTseSignature_WithFiskaly_ReturnsMachineCode()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var signature = await sut.GenerateTseSignatureAsync(sale.Payment!);

        Assert.Equal(FiskalyMachineCode, signature);
    }

    [Fact]
    public async Task GenerateTseSignature_WhenDeviceNotConnected_Throws()
    {
        await using var ctx = CreateContext();
        var (customerId, _, registerId) = await SeedCatalogAsync(ctx);
        var tse = CreateFiskalyTseMock();
        tse.Setup(x => x.GetDeviceStatusAsync())
            .ReturnsAsync(new TseStatus { IsConnected = false, IsReady = false, Status = "Disconnected" });
        var sut = CreatePaymentService(ctx, tse, tseOptions: new TseOptions { TseMode = "Device", Mode = "Production" });

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerName = "Gast",
            CashRegisterId = registerId,
            TotalAmount = 10m,
            TaxAmount = 0.91m,
            PaymentMethodRaw = "0",
            ReceiptNumber = "AT-1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GenerateTseSignatureAsync(payment));
        Assert.Equal("TSE device is not connected", ex.Message);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenNoTseSignature_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            TotalAmount = 10m,
            TaxAmount = 0.91m,
            PaymentMethodRaw = "0",
            TseSignature = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        var sent = await sut.SendToFinanzOnlineAsync(payment);

        Assert.False(sent);
    }

    [Fact]
    public async Task QueryHelpers_AndStatistics_CoverReadPaths()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var byCustomer = await sut.GetCustomerPaymentsAsync(customerId, pageNumber: 0, pageSize: 0);
        Assert.Single(byCustomer);

        var byMethod = await sut.GetPaymentsByMethodAsync("cash", pageNumber: 1, pageSize: 20);
        Assert.Single(byMethod);

        var byDate = await sut.GetPaymentsByDateRangeAsync(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1));
        Assert.Single(byDate);

        var inverted = await sut.GetPaymentsByDateRangeAsync(DateTime.UtcNow.Date.AddDays(1), DateTime.UtcNow.Date.AddDays(-1));
        Assert.Empty(inverted);

        var tooWide = await sut.GetPaymentsByDateRangeAsync(new DateTime(2000, 1, 1), new DateTime(2020, 1, 1));
        Assert.Empty(tooWide);

        var stats = await sut.GetPaymentStatisticsAsync(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1));
        Assert.Equal(1, stats.TotalPayments);
        Assert.Equal(1, stats.TseSignedPayments);

        var loaded = await sut.GetPaymentAsync(sale.Payment!.Id);
        Assert.NotNull(loaded);

        var receipt = await sut.GetReceiptDataAsync(sale.Payment.Id, CashierId);
        Assert.NotNull(receipt);
    }

    [Fact]
    public async Task ConfirmReceiptReprint_WhenReasonMissing_Rejects()
    {
        await using var ctx = CreateContext();
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());

        var result = await sut.ConfirmReceiptReprintAsync(Guid.NewGuid(), request: null, CashierId);

        Assert.False(result.Success);
        Assert.Equal("VALIDATION_MISSING_REASON", result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmReceiptReprint_WithValidReason_Succeeds()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var result = await sut.ConfirmReceiptReprintAsync(
            sale.Payment!.Id,
            new ReceiptReprintRequest { ReprintReasonCode = ReceiptReprintReasonCodes.CustomerRequest },
            CashierId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Receipt);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenSigned_SubmitsInvoice()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var sent = await sut.SendToFinanzOnlineAsync(sale.Payment!);

        Assert.True(sent);
    }

    [Fact]
    public async Task RetryFinanzOnlineSubmit_WhenPaymentExists_ReturnsSubmitResult()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var sale = await sut.CreatePaymentAsync(CashSaleRequest(customerId, productId, registerId), CashierId);
        Assert.True(sale.Success, sale.Message);

        var retry = await sut.RetryFinanzOnlineSubmitAsync(sale.Payment!.Id);

        Assert.True(retry.Success, retry.ErrorMessage);
    }

    [Fact]
    public async Task ComputeBenefitEligibilityPreview_ReturnsCartTotalsWithoutPersisting()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items =
            [
                new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 1 }
            ]
        });

        Assert.NotNull(preview);
        Assert.Equal(10m, preview!.SubtotalBeforeBenefits);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_VoucherMethodWithoutCode_IsDeterministicFailure()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, registerId) = await SeedCatalogAsync(ctx);
        var sut = CreatePaymentService(ctx, CreateFiskalyTseMock());
        var request = CashSaleRequest(customerId, productId, registerId);
        request.Payment = new PaymentMethodRequest { Method = "voucher", TseRequired = true };

        var result = await sut.CreatePaymentAsync(request, CashierId);

        Assert.False(result.Success);
        Assert.Equal(RksvGuardErrorCodes.VoucherCodeRequired, result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
    }
}
