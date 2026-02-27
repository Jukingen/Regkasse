using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Integration test: payment create → receipt fetch → signature-debug → Verify PASS
/// Uses in-memory DB, real TseService + SignaturePipeline.
/// </summary>
public class PaymentReceiptSignatureIntegrationTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PaymentCreate_ReceiptFetch_SignatureVerify_Pass()
    {
        await using var context = CreateInMemoryContext();

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tseService = new TseService(context, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());

        var kassenId = "KASSE-TEST-01";
        var receiptNumber = $"AT-{kassenId}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
        var totalAmount = 99.99m;
        var cashRegisterId = Guid.NewGuid();

        var sigResult = await tseService.CreateInvoiceSignatureAsync(
            cashRegisterId,
            receiptNumber,
            totalAmount,
            kassenId: kassenId,
            taxDetailsJson: "{}");

        Assert.NotNull(sigResult.CompactJws);
        Assert.Equal(3, sigResult.CompactJws.Split('.').Length);

        var valid = await tseService.ValidateTseSignatureAsync(sigResult.CompactJws);
        Assert.True(valid);
    }

    [Fact]
    public async Task ReceiptDTO_ContainsRequiredSignatureFields()
    {
        await using var context = CreateInMemoryContext();

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tseService = new TseService(context, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test GmbH",
            TaxNumber = "ATU12345678",
            Street = "Teststr 1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = "Danke"
        };

        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseService,
            Options.Create(companyProfile));

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true };
        context.Customers.Add(customer);

        var sigResult = await tseService.CreateInvoiceSignatureAsync(
            Guid.NewGuid(),
            "AT-TEST-20250225-12345678",
            50.00m,
            kassenId: "KASSE-01",
            taxDetailsJson: "{}");

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            TableNumber = 1,
            CashierId = "cashier1",
            TotalAmount = 50m,
            TaxAmount = 10m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            KassenId = "KASSE-01",
            TseSignature = sigResult.CompactJws,
            PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed,
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "AT-KASSE-01-20250225-12345678",
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.PaymentDetails.Add(payment);
        await context.SaveChangesAsync();

        var receipt = await receiptService.CreateReceiptFromPaymentAsync(payment.Id);

        Assert.NotNull(receipt.Signature);
        Assert.Equal("ES256", receipt.Signature.Algorithm);
        Assert.False(string.IsNullOrEmpty(receipt.Signature.SerialNumber));
        Assert.False(string.IsNullOrEmpty(receipt.Signature.Timestamp));
        Assert.NotNull(receipt.Signature.PrevSignatureValue);
        Assert.Equal(sigResult.CompactJws, receipt.Signature.SignatureValue);

        var valid = await tseService.ValidateTseSignatureAsync(receipt.Signature.SignatureValue);
        Assert.True(valid);
    }

    /// <summary>
    /// Signature-debug: Payment with valid TseSignature → VerifyDiagnostic all PASS.
    /// </summary>
    [Fact]
    public async Task PaymentWithSignature_VerifyDiagnostic_AllStepsPass()
    {
        await using var context = CreateInMemoryContext();
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tseService = new TseService(context, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());

        var sigResult = await tseService.CreateInvoiceSignatureAsync(
            Guid.NewGuid(), "AT-DIAG-20250225-999", 42.00m, kassenId: "KASSE-DIAG", taxDetailsJson: "{}");

        var steps = pipeline.VerifyDiagnostic(sigResult.CompactJws);
        Assert.Equal(5, steps.Count);
        Assert.All(steps, s => Assert.Equal("PASS", s.Status));
    }
}
