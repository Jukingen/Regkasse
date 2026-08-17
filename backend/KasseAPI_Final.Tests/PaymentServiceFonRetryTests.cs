using System.Reflection;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Comprehensive tests for <see cref="IPaymentService.RetryFinanzOnlineSubmitAsync"/>
/// and the hosted FON retry job (max attempts + exponential backoff).
/// </summary>
public sealed class PaymentServiceFonRetryTests : IAsyncLifetime
{
    private readonly AppDbContext _ctx;
    private readonly Mock<IFinanzOnlineService> _fonService;
    private readonly Mock<ILogger<PaymentService>> _logger;
    private readonly Mock<IFinanzOnlineMetrics> _metrics;
    private readonly Mock<IAuditLogService> _audit;
    private readonly List<AuditRetry> _auditRetries;
    private readonly PaymentService _paymentService;

    public PaymentServiceFonRetryTests()
    {
        _ctx = PaymentServiceCoverageHarness.CreateContext();
        _fonService = new Mock<IFinanzOnlineService>();
        _logger = new Mock<ILogger<PaymentService>>();
        _metrics = new Mock<IFinanzOnlineMetrics>();
        _auditRetries = new List<AuditRetry>();
        _audit = CreateAuditMock(_auditRetries);
        _paymentService = PaymentServiceCoverageHarness.CreatePaymentService(
            _ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Finanz = _fonService,
                FinanzMetrics = _metrics.Object,
                Audit = _audit,
                Logger = _logger.Object
            });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task RetryFONSubmit_WhenFirstAttemptSucceeds_UpdatesStatusToSubmitted()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(Ok("FON-FIRST"));

        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Submitted", result.Status);
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Submitted", stored.FinanzOnlineStatus);
        Assert.Equal("FON-FIRST", stored.FinanzOnlineReferenceId);
        Assert.Equal(1, stored.FinanzOnlineRetryCount);
        Assert.Null(stored.FinanzOnlineError);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Once);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenRetrySucceeds_UpdatesStatusToSubmitted()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.SetupSequence(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(Fail("FON timeout", FinanzOnlineFailureKind.Transient))
            .ReturnsAsync(Ok("FON-RETRY-OK"));

        var first = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.False(first.Success);
        var afterFirst = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Pending", afterFirst.FinanzOnlineStatus);
        Assert.Equal(1, afterFirst.FinanzOnlineRetryCount);

        var second = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.True(second.Success, second.ErrorMessage);
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Submitted", stored.FinanzOnlineStatus);
        Assert.Equal("FON-RETRY-OK", stored.FinanzOnlineReferenceId);
        Assert.Equal(2, stored.FinanzOnlineRetryCount);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RetryFONSubmit_WhenAllRetriesFail_UpdatesStatusToFailed()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(Fail("FON permanently rejected", FinanzOnlineFailureKind.Permanent));

        for (var i = 0; i < 3; i++)
        {
            var attempt = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
            Assert.False(attempt.Success);
            Assert.Equal(FinanzOnlineFailureKind.Permanent, attempt.FailureKind);
        }

        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Failed", stored.FinanzOnlineStatus);
        Assert.Equal(3, stored.FinanzOnlineRetryCount);
        Assert.Equal("FON permanently rejected", stored.FinanzOnlineError);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RetryFONSubmit_WhenRetryExceedsMax_StopsRetrying()
    {
        var due = await SeedJobPaymentAsync(retryCount: 2);
        var atMax = await SeedJobPaymentAsync(retryCount: 3);
        await SetLastAttemptAsync(due, DateTime.UtcNow.AddMinutes(-10));
        await SetLastAttemptAsync(atMax, DateTime.UtcNow.AddMinutes(-10));
        var payment = new Mock<IPaymentService>();
        payment.Setup(x => x.RetryFinanzOnlineSubmitAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Fail("still pending", FinanzOnlineFailureKind.Transient));

        await RunRetryJobCycleAsync(payment.Object, JobOptions(maxRetryCount: 3, baseDelaySeconds: 1));

        payment.Verify(x => x.RetryFinanzOnlineSubmitAsync(due), Times.Once);
        payment.Verify(x => x.RetryFinanzOnlineSubmitAsync(atMax), Times.Never);

        var marked = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == due);
        Assert.Equal("Failed", marked.FinanzOnlineStatus);
        Assert.Contains("Max retries exceeded", marked.FinanzOnlineError);
        var skipped = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == atMax);
        Assert.Equal("Pending", skipped.FinanzOnlineStatus);
    }

    [Fact]
    public async Task RetryFONSubmit_UsesExponentialBackoff()
    {
        Assert.Equal(1, JobBackoffSeconds(0, baseDelaySeconds: 1));
        Assert.Equal(2, JobBackoffSeconds(1, baseDelaySeconds: 1));
        Assert.Equal(4, JobBackoffSeconds(2, baseDelaySeconds: 1));

        var retry0Ready = await SeedJobPaymentAsync(retryCount: 0);
        var retry0TooSoon = await SeedJobPaymentAsync(retryCount: 0);
        var retry1Ready = await SeedJobPaymentAsync(retryCount: 1);
        var retry1TooSoon = await SeedJobPaymentAsync(retryCount: 1);
        var retry2Ready = await SeedJobPaymentAsync(retryCount: 2);
        var retry2TooSoon = await SeedJobPaymentAsync(retryCount: 2);

        var now = DateTime.UtcNow;
        await SetLastAttemptAsync(retry0Ready, now.AddSeconds(-10));
        await SetLastAttemptAsync(retry0TooSoon, now);
        await SetLastAttemptAsync(retry1Ready, now.AddSeconds(-10));
        await SetLastAttemptAsync(retry1TooSoon, now.AddSeconds(-1));
        await SetLastAttemptAsync(retry2Ready, now.AddSeconds(-10));
        await SetLastAttemptAsync(retry2TooSoon, now.AddSeconds(-3));

        var called = new List<Guid>();
        var payment = new Mock<IPaymentService>();
        payment.Setup(x => x.RetryFinanzOnlineSubmitAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
            {
                called.Add(id);
                return Ok($"FON-{id:N}");
            });

        await RunRetryJobCycleAsync(payment.Object, JobOptions(maxRetryCount: 5, baseDelaySeconds: 1));

        Assert.Contains(retry0Ready, called);
        Assert.Contains(retry1Ready, called);
        Assert.Contains(retry2Ready, called);
        Assert.DoesNotContain(retry0TooSoon, called);
        Assert.DoesNotContain(retry1TooSoon, called);
        Assert.DoesNotContain(retry2TooSoon, called);
        payment.Verify(x => x.RetryFinanzOnlineSubmitAsync(It.IsAny<Guid>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RetryFONSubmit_WhenTimeoutError_Retries()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.SetupSequence(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ThrowsAsync(new TaskCanceledException("FON timeout"))
            .ReturnsAsync(Ok("FON-AFTER-TIMEOUT"));

        var timeout = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.False(timeout.Success);
        Assert.Equal(FinanzOnlineFailureKind.Transient, timeout.FailureKind);
        var afterTimeout = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Pending", afterTimeout.FinanzOnlineStatus);
        Assert.Equal(1, afterTimeout.FinanzOnlineRetryCount);

        var recovered = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        Assert.True(recovered.Success, recovered.ErrorMessage);
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Submitted", stored.FinanzOnlineStatus);
        Assert.Equal(2, stored.FinanzOnlineRetryCount);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenAuthError_StopsRetrying()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ThrowsAsync(new InvalidOperationException("Authentication failed: forbidden"));

        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);

        Assert.False(result.Success);
        Assert.Equal(FinanzOnlineFailureKind.Permanent, result.FailureKind);
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Failed", stored.FinanzOnlineStatus);
        Assert.Equal(1, stored.FinanzOnlineRetryCount);
        Assert.Contains("forbidden", stored.FinanzOnlineError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenValidationError_StopsRetrying()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ThrowsAsync(new InvalidOperationException("validation failed: invalid payload"));

        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);

        Assert.False(result.Success);
        Assert.Equal(FinanzOnlineFailureKind.Permanent, result.FailureKind);
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.Equal("Failed", stored.FinanzOnlineStatus);
        Assert.Equal(1, stored.FinanzOnlineRetryCount);
        Assert.Contains("validation", stored.FinanzOnlineError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryFONSubmit_FromPendingState_TransitionsCorrectly()
    {
        var successId = await SeedPaymentAndInvoiceAsync(status: "Pending");
        var failedId = await SeedPaymentAndInvoiceAsync(status: "Pending");
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == successId)))
            .ReturnsAsync(Ok("FON-PENDING-OK"));
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == failedId)))
            .ReturnsAsync(Fail("schema rejected", FinanzOnlineFailureKind.Permanent));

        var success = await _paymentService.RetryFinanzOnlineSubmitAsync(successId);
        var failed = await _paymentService.RetryFinanzOnlineSubmitAsync(failedId);

        Assert.True(success.Success);
        Assert.False(failed.Success);
        Assert.Equal("Submitted", (await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == successId)).FinanzOnlineStatus);
        Assert.Equal("Failed", (await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == failedId)).FinanzOnlineStatus);
    }

    [Fact]
    public async Task RetryFONSubmit_SkippedWhenNotPending()
    {
        var submittedId = await SeedPaymentAndInvoiceAsync(status: "Submitted", referenceId: "REF-KEEP");
        var failedId = await SeedPaymentAndInvoiceAsync(status: "Failed");
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(Ok("FON-SHOULD-NOT-APPLY"));

        var submitted = await _paymentService.RetryFinanzOnlineSubmitAsync(submittedId);
        Assert.True(submitted.Success);
        Assert.Equal("REF-KEEP", submitted.ReferenceId);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == submittedId)), Times.Never);

        var failedRetry = await _paymentService.RetryFinanzOnlineSubmitAsync(failedId);
        Assert.True(failedRetry.Success, failedRetry.ErrorMessage);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == failedId)), Times.Once);
        var afterFailed = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == failedId);
        Assert.Equal("Submitted", afterFailed.FinanzOnlineStatus);
    }

    [Fact]
    public async Task RetryFONSubmit_LogsEachAttempt()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.SetupSequence(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(Fail("FON timeout", FinanzOnlineFailureKind.Transient))
            .ReturnsAsync(Fail("FON timeout again", FinanzOnlineFailureKind.Transient));

        var before = DateTime.UtcNow.AddSeconds(-1);
        await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.Equal(2, _auditRetries.Count);
        Assert.Contains("attempt 1: Failed", _auditRetries[0].Description);
        Assert.Contains("attempt 2: Failed", _auditRetries[1].Description);
        Assert.All(_auditRetries, r => Assert.Equal(AuditLogStatus.Failed, r.Status));
        var stored = await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        Assert.NotNull(stored.FinanzOnlineLastAttemptAtUtc);
        Assert.InRange(stored.FinanzOnlineLastAttemptAtUtc!.Value, before, after);
    }

    [Fact]
    public async Task RetryFONSubmit_LogsFinalOutcome()
    {
        var successId = await SeedPaymentAndInvoiceAsync();
        var failedId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == successId)))
            .ReturnsAsync(Ok("FON-FINAL-OK"));
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.Is<Invoice>(i => i.SourcePaymentId == failedId)))
            .ReturnsAsync(Fail("permanent reject", FinanzOnlineFailureKind.Permanent));

        await _paymentService.RetryFinanzOnlineSubmitAsync(successId);
        await _paymentService.RetryFinanzOnlineSubmitAsync(failedId);

        var successLog = Assert.Single(_auditRetries, r => r.EntityId == successId);
        Assert.Equal(AuditLogStatus.Success, successLog.Status);
        Assert.Contains("Submitted", successLog.Description);
        var failedLog = Assert.Single(_auditRetries, r => r.EntityId == failedId);
        Assert.Equal(AuditLogStatus.Failed, failedLog.Status);
        Assert.Contains("Failed", failedLog.Description);
        Assert.Equal("permanent reject", failedLog.ErrorDetails);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenInvoiceMissing_ReturnsPermanentFailure()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync(withInvoice: false);

        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);

        Assert.False(result.Success);
        Assert.Equal("Invoice not found for payment.", result.ErrorMessage);
        Assert.Equal(FinanzOnlineFailureKind.Permanent, result.FailureKind);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
        _metrics.Verify(m => m.IncrementSubmitTotal(), Times.Once);
        _metrics.Verify(m => m.IncrementSubmitFailed(It.IsAny<FinanzOnlineFailureKind>()), Times.Never);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenUnknownException_MarksFailed()
    {
        var paymentId = await SeedPaymentAndInvoiceAsync();
        _fonService.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ThrowsAsync(new InvalidOperationException("unexpected FON fault"));

        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId);

        Assert.False(result.Success);
        Assert.Equal(FinanzOnlineFailureKind.Unknown, result.FailureKind);
        Assert.Equal("Failed", (await _ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId)).FinanzOnlineStatus);
        _metrics.Verify(m => m.IncrementSubmitTotal(), Times.Once);
        _metrics.Verify(m => m.IncrementSubmitFailed(FinanzOnlineFailureKind.Unknown), Times.Once);
    }

    [Fact]
    public async Task RetryFONSubmit_WhenPaymentNotFound_ReturnsPermanentFailure()
    {
        var result = await _paymentService.RetryFinanzOnlineSubmitAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Payment not found.", result.ErrorMessage);
        Assert.Equal(FinanzOnlineFailureKind.Permanent, result.FailureKind);
        _fonService.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
    }

    private static FinanzOnlineSubmitResponse Ok(string reference) =>
        new()
        {
            Success = true,
            ReferenceId = reference,
            Status = "Submitted",
            SubmittedAt = DateTime.UtcNow,
            FailureKind = FinanzOnlineFailureKind.None
        };

    private static FinanzOnlineSubmitResponse Fail(string message, FinanzOnlineFailureKind kind) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            Status = "Failed",
            SubmittedAt = DateTime.UtcNow,
            FailureKind = kind
        };

    private static FinanzOnlineRetryJobOptions JobOptions(int maxRetryCount, int baseDelaySeconds) =>
        new()
        {
            Enabled = true,
            MaxRetryCount = maxRetryCount,
            BaseDelaySeconds = baseDelaySeconds,
            BackoffCapSeconds = 3600,
            BatchSize = 50,
            Interval = TimeSpan.FromMinutes(2)
        };

    private static int JobBackoffSeconds(int retryCount, int baseDelaySeconds) =>
        Math.Min(baseDelaySeconds * (int)Math.Pow(2, Math.Min(retryCount, 20)), 3600);

    private async Task<Guid> SeedPaymentAndInvoiceAsync(
        string status = "Pending",
        string? referenceId = null,
        bool withInvoice = true)
    {
        TenantTestDoubles.EnsurePlatformTenant(_ctx);
        var registerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        _ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = registerId,
            RegisterNumber = $"R-{registerId.ToString("N")[..8]}",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        _ctx.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Gast",
            Email = "g@t.com",
            Phone = "1",
            IsActive = true
        });
        await _ctx.SaveChangesAsync();

        var paymentId = Guid.NewGuid();
        _ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = customerId,
            CustomerName = "Gast",
            TableNumber = 1,
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "eyJ.eyJ.s",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = $"AT-R1-{paymentId.ToString("N")[..8]}",
            IsPrinted = false,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            FinanzOnlineStatus = status,
            FinanzOnlineError = status == "Submitted" ? null : "seed",
            FinanzOnlineReferenceId = referenceId ?? (status == "Submitted" ? "REF-1" : null),
            FinanzOnlineLastAttemptAtUtc = DateTime.UtcNow,
            FinanzOnlineRetryCount = 0
        });

        if (withInvoice)
        {
            _ctx.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = SystemTenantIds.Platform,
                SourcePaymentId = paymentId,
                InvoiceNumber = $"INV-{paymentId.ToString("N")[..8]}",
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow,
                Status = InvoiceStatus.Paid,
                Subtotal = 9m,
                TaxAmount = 1m,
                TotalAmount = 10m,
                PaidAmount = 10m,
                RemainingAmount = 0,
                CompanyName = "Test GmbH",
                CompanyTaxNumber = "ATU12345678",
                CompanyAddress = "Wien",
                TseSignature = "eyJ.eyJ.s",
                KassenId = "R1",
                TseTimestamp = DateTime.UtcNow,
                CashRegisterId = registerId,
                TaxDetails = JsonDocument.Parse("{}"),
                InvoiceItems = JsonDocument.Parse("[]"),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _ctx.SaveChangesAsync();
        return paymentId;
    }

    private async Task SetLastAttemptAsync(Guid paymentId, DateTime lastAttempt)
    {
        var payment = await _ctx.PaymentDetails.FirstAsync(p => p.Id == paymentId);
        payment.FinanzOnlineLastAttemptAtUtc = lastAttempt;
        await _ctx.SaveChangesAsync();
    }

    private async Task<Guid> SeedJobPaymentAsync(int retryCount)
    {
        TenantTestDoubles.EnsurePlatformTenant(_ctx);
        var paymentId = Guid.NewGuid();
        _ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Job",
            TableNumber = 1,
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = Guid.NewGuid(),
            TseSignature = "eyJ.eyJ.s",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = $"AT-JOB-{paymentId.ToString("N")[..8]}",
            IsPrinted = false,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            FinanzOnlineStatus = "Pending",
            FinanzOnlineError = "seed",
            FinanzOnlineLastAttemptAtUtc = DateTime.UtcNow.AddHours(-1),
            FinanzOnlineRetryCount = retryCount
        });
        await _ctx.SaveChangesAsync();
        return paymentId;
    }

    private async Task RunRetryJobCycleAsync(IPaymentService paymentService, FinanzOnlineRetryJobOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<FinanzOnlineRetryJobOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        var services = new ServiceCollection()
            .AddSingleton(_ctx)
            .AddSingleton(paymentService)
            .BuildServiceProvider();
        var job = new FinanzOnlineRetryHostedService(
            services,
            monitor.Object,
            new FinanzOnlineMetrics(),
            new NoOpFinanzOnlineAlertSink(),
            NullLogger<FinanzOnlineRetryHostedService>.Instance);

        var method = typeof(FinanzOnlineRetryHostedService).GetMethod(
            "RunOneCycleAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        await (Task)method.Invoke(job, new object[] { CancellationToken.None })!;
    }

    private sealed record AuditRetry(Guid? EntityId, string Description, AuditLogStatus Status, string? ErrorDetails);

    private static Mock<IAuditLogService> CreateAuditMock(List<AuditRetry> retries)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .Callback(new InvocationAction(invocation =>
            {
                if ((string)invocation.Arguments[0]! != "FinanzOnlineRetry")
                    return;
                retries.Add(new AuditRetry(
                    (Guid?)invocation.Arguments[2],
                    invocation.Arguments[12] as string ?? "",
                    (AuditLogStatus)invocation.Arguments[14]!,
                    invocation.Arguments[15] as string));
            }))
            .ReturnsAsync(new AuditLog());
        return audit;
    }
}
