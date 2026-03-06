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
/// Integration tests: modifier allowed → payment succeeds and totals include modifiers;
/// disallowed modifier → 400; price tampering → 400.
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
        context.ProductModifiers.Add(new ProductModifier
        {
            Id = modifierId,
            ModifierGroupId = groupId,
            Name = "Ketchup",
            Price = modifierPrice,
            TaxType = 2
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
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((Guid _, string _, decimal _, string? _, string? _) => ("eyJ.eyJ.sign", "prev"));

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "Test", LastName = "User", Role = "Cashier" });

        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { IsOff = false, UseSoftTseWhenNoDevice = true };

        var modifierValidation = new ProductModifierValidationService(context);

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
    public async Task CreatePayment_WithAllowedModifier_SucceedsAndTotalsIncludeModifier()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, modifierId, productPrice, modifierPrice) = SeedData(context);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 0, // Will be computed
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
        // Line: product 6.90 + modifier 0.30 = 7.20 (gross)
        Assert.True(result.Payment.TotalAmount >= productPrice + modifierPrice - 0.01m && result.Payment.TotalAmount <= productPrice + modifierPrice + 0.01m,
            $"TotalAmount {result.Payment.TotalAmount} should include product {productPrice} + modifier {modifierPrice}");
    }

    [Fact]
    public async Task CreatePayment_WithDisallowedModifier_Returns400()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, _, _, _) = SeedData(context);
        var paymentService = CreatePaymentService(context);
        var disallowedModifierId = Guid.NewGuid(); // Not assigned to product

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

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Errors.Any(e => e.Contains(productId.ToString()) && e.Contains(disallowedModifierId.ToString())));
    }

    [Fact]
    public async Task CreatePayment_WithWrongPriceDelta_Returns400()
    {
        await using var context = CreateInMemoryContext();
        var (_, customerId, productId, modifierId, _, _) = SeedData(context);
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
                        new() { ModifierId = modifierId, PriceDelta = 1.00m } // DB has 0.30; tampering
                    }
                }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");

        Assert.False(result.Success);
        Assert.Contains("price", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Errors.Any(e => e.Contains("catalog") || e.Contains("match")));
    }
}
