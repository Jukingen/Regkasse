using System.Text.Json;
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
/// Phase 2: Payment with product-only items (no ModifierIds); add-on product line does not populate PaymentItem.Modifiers.
/// </summary>
public class Phase2PaymentFlatItemsTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentFlat_{Guid.NewGuid()}")
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
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "Test", LastName = "User", Role = "Cashier" });

        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = "Demo" };

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

    /// <summary>CreatePayment with two product-only items (base + add-on) produces PaymentItems with empty Modifiers.</summary>
    [Fact]
    public async Task CreatePayment_WithTwoProductOnlyItems_StoresFlatPaymentItemsWithNoModifiers()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productBaseId = Guid.NewGuid();
        var productAddOnId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productBaseId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        context.Products.Add(new Product
        {
            Id = productAddOnId,
            Name = "Extra Käse",
            Price = 1.50m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true,
            IsSellableAddOn = true
        });
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 8.40m,
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
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
