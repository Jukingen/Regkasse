using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using Microsoft.AspNetCore.Http;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Single pipeline check: ensure-ready (POS session) + <see cref="PaymentService.CreatePaymentAsync"/> share DB state;
/// shift conflict with matching/stale UserSettings assignment must forbid readiness and reject payment on that register id.
/// </summary>
public class CashRegisterConflictPaymentE2EIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegConflictE2E_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal CashierPrincipal() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Cashier) }, "test"));

    private static PosCashRegisterReadinessService CreateReadinessSut(
        AppDbContext ctx,
        ICashRegisterShiftService shift,
        PosCashRegisterFeatureOptions features) =>
        new(
            ctx,
            new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver),
            shift,
            Options.Create(features),
            Mock.Of<ILogger<PosCashRegisterReadinessService>>(),
            TenantTestDoubles.PrimaryTenantResolver);

    private static PaymentService CreatePaymentService(AppDbContext context, ClaimsPrincipal? httpUser = null)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var paymentRepo = new GenericRepository<PaymentDetails>(context, new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object);
        var productRepo = new GenericRepository<Product>(context, new Mock<ILogger<GenericRepository<Product>>>().Object);
        var customerRepo = new GenericRepository<Customer>(context, new Mock<ILogger<GenericRepository<Customer>>>().Object);

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
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

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seqCallCount = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var receiptService = new ReceiptService(context, new Mock<ILogger<ReceiptService>>().Object, tseMock.Object, Options.Create(companyProfile), Mock.Of<IUserService>());
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>())).ReturnsAsync(new AuditLog());

        var cashRegResolver = new CashRegisterResolutionService(context, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
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
            cashRegResolver,
            httpAccessorMock.Object,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    /// <summary>
    /// Sole open register on u2&apos;s shift; u1 has settings pointing at that register (stale/matching assignment).
    /// </summary>
    private static (Guid CustomerId, Guid ProductId, Guid CashRegisterId) SeedSoleOpenConflictWithAssignment(AppDbContext ctx)
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
        const decimal productPrice = 6.90m;

        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        ctx.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = productPrice,
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
        ctx.Customers.Add(new Customer { Id = customerId, Name = "Test Kunde", Email = "t@t.com", Phone = "1", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", Email = "a@test", FirstName = "A", LastName = "B" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2", Email = "b@test", FirstName = "C", LastName = "D" });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = cashRegisterId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
        return (customerId, productId, cashRegisterId);
    }

    [Fact]
    public async Task StaleAssignment_RegisterOpenByOtherUser_EnsureReadyForbidden_And_CreatePaymentRejected()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, regId) = SeedSoleOpenConflictWithAssignment(ctx);

        var shift = new Mock<ICashRegisterShiftService>();
        var readiness = CreateReadinessSut(
            ctx,
            shift.Object,
            new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true, AutoOpenSoleClosedRegister = true });

        var dto = await readiness.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        Assert.Equal("forbidden", dto.NextAction);
        Assert.NotEqual("ready", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterConflict, dto.MessageCode);

        var paymentService = CreatePaymentService(ctx);
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
    }

    /// <summary>
    /// End-to-end guard: HTTP user carries <see cref="AppPermissions.CashRegisterView"/> and <see cref="AppPermissions.PaymentTake"/>;
    /// payment must still fail when the register shift belongs to another user (mirrors resolver unit test for the same policy).
    /// </summary>
    [Fact]
    public async Task CashRegisterView_OnHttpContext_CreatePaymentStillRejected_WhenOtherUserShift()
    {
        await using var ctx = CreateContext();
        var (customerId, productId, regId) = SeedSoleOpenConflictWithAssignment(ctx);

        var paymentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, Roles.Cashier),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterView),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.PaymentTake)
        }, "test"));

        var paymentService = CreatePaymentService(ctx, paymentPrincipal);
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
    }
}
