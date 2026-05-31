using System.Threading;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Payments and receipt-by-payment: effective tenant must match <see cref="PaymentDetails.CashRegisterId"/> owner.
/// </summary>
public sealed class PaymentTenantIsolationTests
{
    private static readonly Guid TenantB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayTenant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static void EnsureTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "pay-isolation-b" });
    }

    private static PaymentService CreatePaymentService(AppDbContext ctx, ISettingsTenantResolver tenantResolver)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var loggerRepo = new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object;
        var loggerProd = new Mock<ILogger<GenericRepository<Product>>>().Object;
        var loggerCust = new Mock<ILogger<GenericRepository<Customer>>>().Object;
        var paymentRepo = new GenericRepository<PaymentDetails>(ctx, loggerRepo);
        var productRepo = new GenericRepository<Product>(ctx, loggerProd);
        var customerRepo = new GenericRepository<Customer>(ctx, loggerCust);
        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert" });
        var finanzMock = new Mock<IFinanzOnlineService>();
        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "u", Role = "Admin" });
        var companyProfile = new CompanyProfileOptions { CompanyName = "T", TaxNumber = "ATU12345678", Street = "S", ZipCode = "1", City = "W", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = "Demo" };
        var receiptService = new ReceiptService(ctx, Mock.Of<ILogger<ReceiptService>>(), tseMock.Object, TenantTestDoubles.CompanyProfileProviderReturning(companyProfile), userMock.Object, tenantResolver);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>())).ReturnsAsync(new AuditLog());
        var cashRegResolver = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), tenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var httpAccessor = Mock.Of<IHttpContextAccessor>();
        return new PaymentService(ctx, paymentRepo, productRepo, customerRepo, tseMock.Object, finanzMock.Object, userMock.Object, new NoOpProductModifierValidationService(), Mock.Of<IReceiptSequenceService>(), receiptService, auditMock.Object, TenantTestDoubles.CompanyProfileProviderReturning(companyProfile), Options.Create(tseOptions), Options.Create(new InventoryOptions()), loggerPayment, cashRegResolver, httpAccessor, new PaymentMethodCatalogService(ctx, tenantResolver), new PricingRuleResolver(ctx, tenantResolver), tenantResolver);
    }

    private static PaymentDetails MinimalPayment(Guid id, Guid customerId, Guid cashRegisterId, DateTime createdAt)
    {
        return new PaymentDetails
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = createdAt,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "R1",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsActive = true
        };
    }

    private static async Task<(Guid custA, Guid regA, Guid custB, Guid regB, Guid payB)> SeedTwoTenantPaymentsAsync(AppDbContext ctx)
    {
        EnsureTenants(ctx);
        var custA = Guid.NewGuid();
        var custB = Guid.NewGuid();
        ctx.Customers.Add(new Customer { Id = custA, Name = "A", Email = "a@a.com", Phone = "1", IsActive = true });
        ctx.Customers.Add(new Customer { Id = custB, Name = "B", Email = "b@b.com", Phone = "2", IsActive = true });
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister { TenantId = LegacyDefaultTenantIds.Primary, Id = regA, RegisterNumber = "KA", Location = "L", StartingBalance = 0, CurrentBalance = 0, LastBalanceUpdate = DateTime.UtcNow, Status = RegisterStatus.Open, CreatedAt = DateTime.UtcNow, IsActive = true });
        ctx.CashRegisters.Add(new CashRegister { TenantId = TenantB, Id = regB, RegisterNumber = "KB", Location = "L", StartingBalance = 0, CurrentBalance = 0, LastBalanceUpdate = DateTime.UtcNow, Status = RegisterStatus.Open, CreatedAt = DateTime.UtcNow, IsActive = true });
        var day = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var payA = Guid.NewGuid();
        var payB = Guid.NewGuid();
        ctx.PaymentDetails.Add(MinimalPayment(payA, custA, regA, day));
        ctx.PaymentDetails.Add(MinimalPayment(payB, custB, regB, day));
        await ctx.SaveChangesAsync();
        return (custA, regA, custB, regB, payB);
    }

    [Fact]
    public async Task Admin_GetList_AsTenantA_ExcludesTenantBPayment()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var mockPay = new Mock<IPaymentService>(MockBehavior.Strict);
        var resolver = TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary);
        var c = TenantTestDoubles.CreateAdminPaymentsController(ctx, resolver, mockPay.Object);
        var result = await c.GetPayments(
            new PaymentFilterDto { StartDate = new DateTime(2026, 3, 15), EndDate = new DateTime(2026, 3, 15) },
            cancellationToken: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<PaymentListResponse>(ok.Value);
        Assert.DoesNotContain(body.Items, x => x.Id == payB);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task Admin_GetDetail_OtherTenantPayment_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var mockPay = new Mock<IPaymentService>(MockBehavior.Strict);
        var resolver = TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary);
        var c = TenantTestDoubles.CreateAdminPaymentsController(ctx, resolver, mockPay.Object);
        var result = await c.GetDetail(payB, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPaymentStatistics_OnlyIncludesEffectiveTenant()
    {
        await using var ctx = CreateContext();
        await SeedTwoTenantPaymentsAsync(ctx);
        var svc = CreatePaymentService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var stats = await svc.GetPaymentStatisticsAsync(new DateTime(2026, 3, 15), new DateTime(2026, 3, 15));
        Assert.Equal(1, stats.TotalPayments);
        Assert.Equal(10m, stats.TotalAmount);
    }

    [Fact]
    public async Task GetPaymentAsync_OtherTenant_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var svc = CreatePaymentService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var p = await svc.GetPaymentAsync(payB);
        Assert.Null(p);
    }

    [Fact]
    public async Task CancelPaymentAsync_OtherTenant_ReturnsNotFound()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var svc = CreatePaymentService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var r = await svc.CancelPaymentAsync(payB, "reason", "u1");
        Assert.False(r.Success);
        Assert.Equal("Payment not found", r.Message);
    }

    [Fact]
    public async Task RefundPaymentAsync_OtherTenant_ReturnsNotFound()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var svc = CreatePaymentService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var r = await svc.RefundPaymentAsync(payB, 1m, "reason", "u1");
        Assert.False(r.Success);
        Assert.Equal("Payment not found", r.Message);
    }

    [Fact]
    public async Task Receipt_GetByPaymentId_OtherTenant_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var (_, _, _, regB, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        ctx.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = payB,
            ReceiptNumber = "RB",
            IssuedAt = DateTime.UtcNow,
            CashierId = "c",
            CashRegisterId = regB,
            SubTotal = 9,
            TaxTotal = 1,
            GrandTotal = 10,
            QrCodePayload = "",
            SignatureValue = "",
            PrevSignatureValue = "",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var receiptSvc = new ReceiptService(ctx, Mock.Of<ILogger<ReceiptService>>(), Mock.Of<ITseService>(), TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions { CompanyName = "T", TaxNumber = "ATU12345678", Street = "S", ZipCode = "1", City = "W", FooterText = "" }), Mock.Of<IUserService>(), TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var dto = await receiptSvc.GetReceiptByPaymentIdAsync(payB);
        Assert.Null(dto);
    }

    [Fact]
    public async Task RetryFinanzOnlineSubmitAsync_OtherTenant_ReturnsNotFoundMessage()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, payB) = await SeedTwoTenantPaymentsAsync(ctx);
        var svc = CreatePaymentService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));
        var r = await svc.RetryFinanzOnlineSubmitAsync(payB);
        Assert.False(r.Success);
        Assert.Equal("Payment not found.", r.ErrorMessage);
    }
}
