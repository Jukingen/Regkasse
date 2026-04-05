using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

/// <summary>
/// <para><strong>Temporal correctness (server)</strong>: <see cref="PaymentService.CreatePaymentAsync"/> calls
/// <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterAsync"/> (T1, pre-transaction) then later
/// <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterForCommitAsync"/> inside the fiscal transaction (T2).
/// These tests prove the commit gate can fail after a successful pre-check — no sleeps; Moq simulates state change between phases.</para>
/// <para><strong>Snapshot-only gaps</strong>: A single call to <c>ValidatePaymentRegisterAsync</c> does not prove rollback.
/// PostgreSQL lifecycle tests (<see cref="PostgreSqlCashRegisterPaymentLifecycleTests"/>) use real DB state between separate calls / locks.</para>
/// <para><strong>Matrix</strong>: (1) pre Ok + commit Closed → no payment; (2) pre Ok + commit Forbidden → no payment;
/// real resolver + dormant row → <see cref="PosCashRegisterPaymentPolicyIntegrationTests"/>.</para>
/// </summary>
public class PaymentRegisterCommitGateTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayCommitGate_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentService CreatePaymentService(
        AppDbContext context,
        ICashRegisterResolutionService cashRegisterResolution,
        out Mock<IReceiptSequenceService> receiptSeqMock,
        ClaimsPrincipal? httpUser = null)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var paymentRepo = new GenericRepository<PaymentDetails>(context, new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object);
        var productRepo = new GenericRepository<Product>(context, new Mock<ILogger<GenericRepository<Product>>>().Object);
        var customerRepo = new GenericRepository<Customer>(context, new Mock<ILogger<GenericRepository<Customer>>>().Object);

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync("u1")).ReturnsAsync(new ApplicationUser
        {
            Id = "u1",
            UserName = "cashier",
            FirstName = "Test",
            LastName = "User",
            Role = "Cashier"
        });

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };
        var tseOptions = new TseOptions { TseMode = "Demo" };

        receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seqCallCount = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var receiptService = new ReceiptService(context, new Mock<ILogger<ReceiptService>>().Object, tseMock.Object, Options.Create(companyProfile), Mock.Of<IUserService>(), TenantTestDoubles.PrimaryTenantResolver);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        if (httpUser != null)
            httpContext.User = httpUser;
        httpAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

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
            auditMock.Object,
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            loggerPayment,
            cashRegisterResolution,
            httpAccessorMock.Object,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    private static (Guid CustomerId, Guid ProductId, Guid CashRegisterId) SeedSoleOpenRegisterU1(AppDbContext ctx)
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        ctx.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = 6.90m,
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
        ctx.Customers.Add(new Customer { Id = customerId, Name = "K", Email = "k@test", Phone = "1", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = cashRegisterId,
            RegisterNumber = "K01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.SaveChanges();
        return (customerId, productId, cashRegisterId);
    }

    [Fact]
    public async Task CreatePaymentAsync_PreCheckPasses_CommitGateClosed_NoPaymentPersisted_AllocateNotCalled()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, regId) = SeedSoleOpenRegisterU1(ctx);

        var mockResolution = new Mock<ICashRegisterResolutionService>();
        mockResolution
            .Setup(x => x.ValidatePaymentRegisterAsync("u1", regId, It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Success(regId, "K01"));
        mockResolution
            .Setup(x => x.ValidatePaymentRegisterForCommitAsync("u1", regId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Closed,
                "Cash register is closed or not usable for payment."));

        var paymentService = CreatePaymentService(ctx, mockResolution.Object, out var receiptSeqMock);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Closed, result.DiagnosticCode);
        mockResolution.Verify(
            x => x.ValidatePaymentRegisterForCommitAsync("u1", regId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
        receiptSeqMock.Verify(
            x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);
        Assert.False(await ctx.PaymentDetails.AnyAsync());
    }

    /// <summary>
    /// Temporal: T1 pre-check authorizes; T2 commit gate rejects shift/occupancy — same shape as register reopening under another user mid-flight.
    /// </summary>
    [Fact]
    public async Task CreatePaymentAsync_PreCheckPasses_CommitGateForbiddenShift_NoPaymentPersisted_AllocateNotCalled()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, regId) = SeedSoleOpenRegisterU1(ctx);

        var mockResolution = new Mock<ICashRegisterResolutionService>();
        mockResolution
            .Setup(x => x.ValidatePaymentRegisterAsync("u1", regId, It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Success(regId, "K01"));
        mockResolution
            .Setup(x => x.ValidatePaymentRegisterForCommitAsync("u1", regId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Forbidden,
                "Cash register is in use by another user."));

        var paymentService = CreatePaymentService(ctx, mockResolution.Object, out var receiptSeqMock);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, result.DiagnosticCode);
        receiptSeqMock.Verify(
            x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);
        Assert.False(await ctx.PaymentDetails.AnyAsync());
    }
}
