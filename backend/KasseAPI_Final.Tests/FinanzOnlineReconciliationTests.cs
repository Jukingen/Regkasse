using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// FinanzOnline reconciliation: retry, duplicate/idempotent retry when already submitted. State update after submit (full payment flow covered by integration).
/// </summary>
public class FinanzOnlineReconciliationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"FORecon_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentService CreatePaymentService(AppDbContext context, Mock<IFinanzOnlineService> finanzMock)
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
        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "T", LastName = "U", Role = "Cashier" });
        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = "Demo" };
        var modifierValidation = new NoOpProductModifierValidationService();
        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seq = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(It.IsAny<IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string r, DateTime d) => $"AT-{r}-{d:yyyyMMdd}-{++seq}");
        var loggerReceipt = new Mock<ILogger<ReceiptService>>().Object;
        var receiptService = new ReceiptService(context, loggerReceipt, tseMock.Object, Options.Create(companyProfile));
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>())).ReturnsAsync(new AuditLog());
        return new PaymentService(context, paymentRepo, productRepo, customerRepo, tseMock.Object, finanzMock.Object, userMock.Object, modifierValidation, receiptSeqMock.Object, receiptService, auditMock.Object, Options.Create(companyProfile), Options.Create(tseOptions), loggerPayment);
    }

    /// <summary>Manually seed payment + invoice for retry tests (avoids full CreatePayment InMemory transaction/ReceiptService setup).</summary>
    private static async Task<(Guid paymentId, Guid invoiceId)> SeedPaymentAndInvoiceAsync(AppDbContext context, string finanzOnlineStatus = "Pending")
    {
        var regId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister { Id = regId, RegisterNumber = "R1", Location = "L", StartingBalance = 0, CurrentBalance = 0, LastBalanceUpdate = DateTime.UtcNow, Status = RegisterStatus.Open, CreatedAt = DateTime.UtcNow, IsActive = true });
        context.Customers.Add(new Customer { Id = customerId, Name = "C", Email = "c@c.com", Phone = "1", IsActive = true });
        await context.SaveChangesAsync();

        var paymentId = Guid.NewGuid();
        var payment = new PaymentDetails
        {
            Id = paymentId,
            CustomerId = customerId,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.s",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "AT-R1-20260319-1",
            IsPrinted = false,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            FinanzOnlineStatus = finanzOnlineStatus,
            FinanzOnlineError = finanzOnlineStatus != "Submitted" ? "Test error" : null,
            FinanzOnlineReferenceId = finanzOnlineStatus == "Submitted" ? "REF-1" : null,
            FinanzOnlineLastAttemptAtUtc = DateTime.UtcNow,
            FinanzOnlineRetryCount = 0
        };
        context.PaymentDetails.Add(payment);
        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            SourcePaymentId = paymentId,
            InvoiceNumber = payment.ReceiptNumber,
            InvoiceDate = payment.CreatedAt,
            DueDate = payment.CreatedAt,
            Status = InvoiceStatus.Paid,
            Subtotal = 9m,
            TaxAmount = 1m,
            TotalAmount = 10m,
            PaidAmount = 10m,
            RemainingAmount = 0,
            CompanyName = "Test",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "A",
            TseSignature = payment.TseSignature,
            KassenId = "R1",
            TseTimestamp = payment.TseTimestamp,
            CashRegisterId = regId,
            TaxDetails = JsonDocument.Parse("{}"),
            InvoiceItems = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (paymentId, invoiceId);
    }

    /// <summary>Retry after initial failure → submit succeeds, status becomes Submitted and retry count increments.</summary>
    [Fact]
    public async Task RetryFinanzOnlineSubmit_AfterFailure_SucceedsAndUpdatesStatus()
    {
        await using var context = CreateContext();
        var (paymentId, _) = await SeedPaymentAndInvoiceAsync(context, "Pending");

        var successResponse = new FinanzOnlineSubmitResponse { Success = true, ReferenceId = "REF-2", Status = "Submitted", SubmittedAt = DateTime.UtcNow, FailureKind = FinanzOnlineFailureKind.None };
        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(successResponse);
        var paymentService = CreatePaymentService(context, finanzMock);

        var retryResult = await paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.True(retryResult.Success);
        Assert.Equal("REF-2", retryResult.ReferenceId);

        var after = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Submitted", after.FinanzOnlineStatus);
        Assert.Equal("REF-2", after.FinanzOnlineReferenceId);
        Assert.Equal(1, after.FinanzOnlineRetryCount);
    }

    /// <summary>When status is already Submitted, retry returns success without calling external submit (idempotent, duplicate risk avoided).</summary>
    [Fact]
    public async Task RetryFinanzOnlineSubmit_WhenAlreadySubmitted_ReturnsSuccessWithoutResubmitting()
    {
        await using var context = CreateContext();
        var (paymentId, _) = await SeedPaymentAndInvoiceAsync(context, "Submitted");

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });
        var paymentService = CreatePaymentService(context, finanzMock);

        var retryResult = await paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.True(retryResult.Success);
        Assert.Equal("REF-1", retryResult.ReferenceId);

        finanzMock.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
    }

    /// <summary>Retry when payment not found returns failure.</summary>
    [Fact]
    public async Task RetryFinanzOnlineSubmit_WhenPaymentNotFound_ReturnsFailure()
    {
        await using var context = CreateContext();
        var finanzMock = new Mock<IFinanzOnlineService>();
        var paymentService = CreatePaymentService(context, finanzMock);

        var result = await paymentService.RetryFinanzOnlineSubmitAsync(Guid.NewGuid());
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage ?? "");
        finanzMock.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
    }
}
