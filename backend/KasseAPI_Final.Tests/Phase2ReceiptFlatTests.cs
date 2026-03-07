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
/// Phase 2: Receipt from flat payment = one line per item; receipt from legacy payment with Modifiers = nested lines.
/// </summary>
public class Phase2ReceiptFlatTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReceiptFlat_{Guid.NewGuid()}")
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
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", Role = "Cashier" });
        var tseOptions = new TseOptions { TseMode = "Demo" };
        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var modifierValidation = new NoOpProductModifierValidationService();
        return new PaymentService(context, paymentRepo, productRepo, customerRepo, tseMock.Object, finanzMock.Object, userMock.Object, modifierValidation, Options.Create(companyProfile), Options.Create(tseOptions), loggerPayment);
    }

    [Fact]
    public async Task GetReceiptData_FromFlatPayment_ReturnsOneLinePerItemNoModifierLines()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product { Id = product1Id, Name = "Döner", Price = 6.90m, CategoryId = categoryId, Category = "Speisen", StockQuantity = 10, MinStockLevel = 0, Unit = "Stk", TaxType = 2, IsActive = true });
        context.Products.Add(new Product { Id = product2Id, Name = "Extra Käse", Price = 1.50m, CategoryId = categoryId, Category = "Speisen", StockQuantity = 0, MinStockLevel = 0, Unit = "Stk", TaxType = 2, IsActive = true, IsSellableAddOn = true });
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
                new() { ProductId = product1Id, Quantity = 1, TaxType = TaxType.Reduced },
                new() { ProductId = product2Id, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var createResult = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.PaymentId);

        var paymentId = createResult.PaymentId.Value;
        var receipt = await paymentService.GetReceiptDataAsync(paymentId);

        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Equal(2, receipt.Items.Count);
        Assert.All(receipt.Items, item => Assert.False(item.IsModifierLine));
        Assert.Contains(receipt.Items, i => i.Name == "Döner");
        Assert.Contains(receipt.Items, i => i.Name == "Extra Käse");
    }

    /// <summary>Add-on products contribute to price totals and tax totals like normal products.</summary>
    [Fact]
    public async Task CreatePayment_WithBaseProductAndAddOn_PriceAndTaxTotalsCorrect()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product { Id = product1Id, Name = "Burger", Price = 9.90m, CategoryId = categoryId, Category = "Speisen", StockQuantity = 10, MinStockLevel = 0, Unit = "Stk", TaxType = 2, IsActive = true });
        context.Products.Add(new Product { Id = product2Id, Name = "Ketchup", Price = 0.50m, CategoryId = categoryId, Category = "Extras", StockQuantity = 0, MinStockLevel = 0, Unit = "Stk", TaxType = 2, IsActive = true, IsSellableAddOn = true });
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10.90m, // 9.90 + 0.50*2 = 10.90 (gross, VAT included)
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = product1Id, Quantity = 1, TaxType = TaxType.Reduced },
                new() { ProductId = product2Id, Quantity = 2, TaxType = TaxType.Reduced }
            }
        };

        var createResult = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.PaymentId);

        var receipt = await paymentService.GetReceiptDataAsync(createResult.PaymentId!.Value);
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);

        var burgerLine = receipt.Items.FirstOrDefault(i => i.Name == "Burger");
        var ketchupLine = receipt.Items.FirstOrDefault(i => i.Name == "Ketchup");
        Assert.NotNull(burgerLine);
        Assert.NotNull(ketchupLine);
        Assert.Equal(1, burgerLine.Quantity);
        Assert.Equal(2, ketchupLine.Quantity);
        Assert.True(burgerLine.TotalPrice >= 9.80m && burgerLine.TotalPrice <= 10.00m);
        Assert.True(ketchupLine.TotalPrice >= 0.98m && ketchupLine.TotalPrice <= 1.12m);
        Assert.True(receipt.GrandTotal >= 10.80m && receipt.GrandTotal <= 12.00m);
    }

    [Fact]
    public async Task GetReceiptData_FromPaymentCreatedWithModifierIds_Phase3Ignores_ReturnsProductOnlyLine()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        context.Categories.Add(new Category { Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product { Id = productId, Name = "Döner", Price = 6.90m, CategoryId = categoryId, Category = "Speisen", StockQuantity = 10, MinStockLevel = 0, Unit = "Stk", TaxType = 2, IsActive = true });
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        context.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, Name = "Saucen", SortOrder = 0, IsActive = true });
        context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = productId, ModifierGroupId = groupId, SortOrder = 0 });
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var createResult = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.PaymentId);

        var receipt = await paymentService.GetReceiptDataAsync(createResult.PaymentId.Value);

        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Items);
        Assert.Single(receipt.Items);
        Assert.False(receipt.Items[0].IsModifierLine);
        Assert.Contains("Döner", receipt.Items[0].Name);
    }
}
