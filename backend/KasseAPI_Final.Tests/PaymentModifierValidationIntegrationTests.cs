using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Integration tests for payment. Phase 3 prep: ModifierIds/Modifiers are ignored for write; payment succeeds with product-only totals. Historical validation tests updated to expect ignore behavior.
/// </summary>
public class PaymentModifierValidationIntegrationTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentModifier_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static (AppDbContext Context, Guid CustomerId, Guid ProductId, Guid ModifierId, decimal ProductPrice, decimal ModifierPrice) SeedData(AppDbContext context)
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var productPrice = 6.90m;
        var modifierPrice = 0.30m;

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = productPrice,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.Customers.Add(new Customer { Id = customerId, Name = "Test Kunde", Email = "t@t.com", Phone = "1", IsActive = true });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0 });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productId,
            ModifierGroupId = groupId,
            SortOrder = 0
        });
        context.SaveChanges();
        return (context, customerId, productId, modifierId, productPrice, modifierPrice);
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
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "Test", LastName = "User", Role = "Cashier" });

        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = "Demo" }; // UseSoftTseWhenNoDevice = true for tests

        var modifierValidation = new NoOpProductModifierValidationService();

        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            modifierValidation,
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            loggerPayment);
    }

    [Fact]
    public async Task CreatePayment_WithModifierIds_Phase3IgnoresModifiers_TotalsProductOnly()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, modifierId, productPrice, _) = SeedData(context);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 0,
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 1,
                    TaxType = TaxType.Reduced,
                    ModifierIds = new List<Guid> { modifierId }
                }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.NotNull(result.Payment);
        // Phase 3: ModifierIds ignored; total = product only
        Assert.True(result.Payment.TotalAmount >= productPrice - 0.01m && result.Payment.TotalAmount <= productPrice + 0.01m,
            $"TotalAmount {result.Payment.TotalAmount} should be product-only {productPrice} (Phase 3)");
    }

    [Fact]
    public async Task CreatePayment_WithDisallowedModifierIds_Phase3Ignores_SucceedsWithProductOnly()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, _, productPrice, _) = SeedData(context);
        var paymentService = CreatePaymentService(context);
        var disallowedModifierId = Guid.NewGuid();

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 1,
                    TaxType = TaxType.Reduced,
                    ModifierIds = new List<Guid> { disallowedModifierId }
                }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.True(result.Success);
        Assert.NotNull(result.Payment);
        Assert.True(result.Payment.TotalAmount >= productPrice - 0.01m && result.Payment.TotalAmount <= productPrice + 0.01m);
    }

    [Fact]
    public async Task CreatePayment_WithWrongPriceDeltaInModifiers_Phase3Ignores_SucceedsWithProductOnly()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, modifierId, productPrice, _) = SeedData(context);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 1,
                    TaxType = TaxType.Reduced,
                    Modifiers = new List<PaymentItemModifierRequest>
                    {
                        new() { ModifierId = modifierId, PriceDelta = 1.00m }
                    }
                }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.True(result.Success);
        Assert.NotNull(result.Payment);
        Assert.True(result.Payment.TotalAmount >= productPrice - 0.01m && result.Payment.TotalAmount <= productPrice + 0.01m);
    }
}
