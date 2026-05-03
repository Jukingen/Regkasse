using System.Text.Json;
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
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Payment with product-only items (no ModifierIds); add-on product line does not populate PaymentItem.Modifiers.
/// </summary>
public class Phase2PaymentFlatItemsTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentFlat_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentService CreatePaymentService(AppDbContext context)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var loggerRepo = new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object;
        var loggerProd = new Mock<ILogger<GenericRepository<Product>>>().Object;
        var loggerCust = new Mock<ILogger<GenericRepository<Customer>>>().Object;

        var paymentRepo = new GenericRepository<PaymentDetails>(context, loggerRepo);
        var productRepo = new GenericRepository<Product>(context, loggerProd);
        var customerRepo = new GenericRepository<Customer>(context, loggerCust);

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "Test", LastName = "User", Role = "Cashier" });

        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = "Demo" };

        var modifierValidation = new NoOpProductModifierValidationService();

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seqCallCount = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var loggerReceipt = new Mock<ILogger<ReceiptService>>().Object;
        var receiptService = new ReceiptService(context, loggerReceipt, tseMock.Object, Options.Create(companyProfile), Mock.Of<IUserService>(), TenantTestDoubles.PrimaryTenantResolver);

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>())).ReturnsAsync(new AuditLog());
        var cashRegResolver = new CashRegisterResolutionService(context, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var httpAccessor = Mock.Of<IHttpContextAccessor>();
        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            modifierValidation,
            receiptSeqMock.Object,
            receiptService,
            auditMock.Object,
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            Options.Create(new InventoryOptions()),
            loggerPayment,
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    /// <summary>CreatePayment with two product-only items (base + add-on) produces PaymentItems with empty Modifiers.</summary>
    [Fact]
    public async Task CreatePayment_WithTwoProductOnlyItems_StoresFlatPaymentItemsWithNoModifiers()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productBaseId = Guid.NewGuid();
        var productAddOnId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productBaseId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productBaseId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        context.Products.Add(new Product
        {
            Id = productAddOnId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Extra Käse",
            Price = 1.50m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productAddOnId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            IsSellableAddOn = true
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

        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 8.40m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productBaseId, Quantity = 1, TaxType = TaxType.Reduced },
                new() { ProductId = productAddOnId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.NotNull(result.Payment);

        var payment = await context.PaymentDetails.FirstOrDefaultAsync(p => p.Id == result.Payment.Id);
        Assert.NotNull(payment);
        Assert.NotNull(payment.PaymentItems);

        var json = payment.PaymentItems.RootElement.GetRawText();
        var items = JsonSerializer.Deserialize<List<PaymentItem>>(json);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        foreach (var item in items)
        {
            Assert.True((item.Modifiers?.Count ?? 0) == 0, "Flat add-on items should have no Modifiers in PaymentItems JSON");
        }
        Assert.True(result.Payment.TotalAmount >= 8.30m && result.Payment.TotalAmount <= 8.50m);
    }
}
