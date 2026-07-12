using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

/// <summary>
/// <see cref="InventoryOptions.EnforceStockOnSales"/> gates payment stock check/deduct (and keeps storno/refund symmetric).
/// </summary>
public sealed class PaymentServiceStockEnforcementTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"StockEnf_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentService CreatePaymentService(AppDbContext context, InventoryOptions inventoryOptions)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", Role = "Cashier" });

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        var cashRegResolver = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());

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
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(tseOptions),
            Options.Create(inventoryOptions),
            loggerPayment,
            cashRegResolver,
            Mock.Of<IHttpContextAccessor>(),
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    [Fact]
    public async Task CreatePayment_WhenEnforceStockOnSalesTrue_AndStockInsufficient_Rejects()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "OutOfStock",
            Price = 5m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 0,
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
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        var regId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var sut = CreatePaymentService(context, new InventoryOptions { EnforceStockOnSales = true });
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await sut.CreatePaymentAsync(request, "u1");

        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Message);
        var product = await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(0, product.StockQuantity);
    }

    [Fact]
    public async Task CreatePayment_WhenEnforceStockOnSalesFalse_AndStockInsufficient_Succeeds_WithoutMutatingStock()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "OutOfStock",
            Price = 5m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 0,
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
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        var regId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var sut = CreatePaymentService(context, new InventoryOptions { EnforceStockOnSales = false });
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await sut.CreatePaymentAsync(request, "u1");

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var product = await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(0, product.StockQuantity);
    }
}
