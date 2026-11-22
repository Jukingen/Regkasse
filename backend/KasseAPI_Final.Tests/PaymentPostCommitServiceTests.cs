using System.Reflection;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentPostCommitServiceTests
{
    [Fact]
    public async Task Dispatch_AfterCommit_AuditsReceiptAndSubmitsFinanzOnline_ThenLinksCard()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = Payment();
        var invoice = new Invoice { Id = Guid.NewGuid() };
        ctx.Receipts.Add(new Receipt
        {
            TenantId = SystemTenantIds.Platform,
            PaymentId = payment.Id,
            ReceiptNumber = payment.ReceiptNumber,
            CashRegisterId = payment.CashRegisterId,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = payment.TotalAmount
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var steps = new List<string>();
        var intentId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(
            Guid.NewGuid(), Guid.NewGuid(), payment.CashRegisterId, cardPaymentIntentId: intentId);
        var foResult = new FinanzOnlineSubmitResponse { Success = true, ReferenceId = "FON-1", Status = "Submitted" };
        var metrics = new Mock<IFinanzOnlineMetrics>();
        var submission = new Mock<IPaymentFinanzOnlineSubmissionService>();
        submission.Setup(service => service.UpdatePaymentFinanzOnlineStateAsync(
                payment.Id, foResult, false, "cashier", null))
            .Returns(Task.CompletedTask);
        var audit = Audit(steps);
        var sut = Create(
            ctx,
            audit,
            Finanz(invoice, foResult, steps),
            submission.Object,
            metrics.Object,
            Card(intentId, payment.Id, steps),
            (action, _, _, _, _, _, _, _, _, _) =>
            {
                steps.Add(action);
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                steps.Add("qr");
                return Task.FromResult(("qr-payload", false, "Device"));
            });

        var result = await sut.DispatchPostCommitAsync(
            request,
            "cashier",
            new Customer { Id = Guid.NewGuid(), Name = "Ada" },
            Guid.NewGuid(),
            payment,
            invoice,
            new[] { new IssuedTicketDto { Code = "T1", DisplayCode = "T-1" } },
            paymentStockLinesSkipped: 0,
            effectiveTseRequired: true,
            offlineReplayBatchCorrelationId: null);

        Assert.True(result.Success);
        Assert.Equal("Payment created successfully", result.Message);
        Assert.Equal(payment.Id, result.PaymentId);
        Assert.Equal("sig", result.TseSignature);
        Assert.Equal("qr-payload", result.QrPayload);
        Assert.False(result.IsDemoFiscal);
        Assert.Equal("Device", result.TseProvider);
        Assert.True(result.InvoicePersisted);
        Assert.True(result.TimeSyncWarning);
        Assert.Equal(
            new[] { "PaymentCreated", "ReceiptPersisted", "fon", "PAYMENT_WITH_PRESCRIPTION", "TICKET_ISSUED", "card", "qr" },
            steps);
        metrics.Verify(m => m.IncrementSubmitTotal(), Times.Once);
        metrics.Verify(m => m.IncrementSubmitFailed(It.IsAny<FinanzOnlineFailureKind>()), Times.Never);
        Assert.Null(ctx.Database.CurrentTransaction);
        Assert.Empty(ctx.ChangeTracker.Entries());
        Assert.Equal(0, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.Equal(1, await ctx.Receipts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Dispatch_WhenFinanzOnlineThrows_RecordsFailureAndStillReturnsSuccess()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = Payment();
        var invoice = new Invoice { Id = Guid.NewGuid() };
        var intentId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(
            Guid.NewGuid(), Guid.NewGuid(), payment.CashRegisterId, cardPaymentIntentId: intentId);
        var fon = new Mock<IFinanzOnlineService>();
        fon.Setup(service => service.SubmitInvoiceAsync(invoice))
            .ThrowsAsync(new InvalidOperationException("fon down"));
        var metrics = new Mock<IFinanzOnlineMetrics>();
        var submission = new Mock<IPaymentFinanzOnlineSubmissionService>();
        submission.Setup(service => service.UpdatePaymentFinanzOnlineStateAsync(
                payment.Id,
                It.Is<FinanzOnlineSubmitResponse>(response =>
                    !response.Success
                    && response.Status == "Failed"
                    && response.FailureKind == FinanzOnlineFailureKind.Unknown
                    && response.ErrorMessage == "fon down"),
                false,
                "cashier",
                null))
            .Returns(Task.CompletedTask);
        var cardLinked = false;
        var sut = Create(
            ctx,
            Audit(new List<string>()),
            fon.Object,
            submission.Object,
            metrics.Object,
            Card(intentId, payment.Id, onLink: () => cardLinked = true),
            (_, _, _, _, _, _, _, _, _, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(("qr-payload", false, "Device")));

        var result = await sut.DispatchPostCommitAsync(
            request, "cashier", new Customer { Name = "Ada" }, Guid.NewGuid(),
            payment, invoice, Array.Empty<IssuedTicketDto>(), 0, true, null);

        Assert.True(result.Success);
        Assert.Equal("qr-payload", result.QrPayload);
        Assert.True(cardLinked);
        metrics.Verify(m => m.IncrementSubmitTotal(), Times.Once);
        metrics.Verify(m => m.IncrementSubmitFailed(FinanzOnlineFailureKind.Unknown), Times.Once);
        submission.VerifyAll();
    }

    [Fact]
    public async Task Dispatch_WhenPaymentAuditThrows_SwallowsAndStillLinksCard()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = Payment();
        var invoice = new Invoice { Id = Guid.NewGuid() };
        var intentId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(
            Guid.NewGuid(), Guid.NewGuid(), payment.CashRegisterId, cardPaymentIntentId: intentId);
        var fon = new Mock<IFinanzOnlineService>();
        var cardLinked = false;
        var sut = Create(
            ctx,
            Audit(new List<string>()),
            fon.Object,
            new Mock<IPaymentFinanzOnlineSubmissionService>().Object,
            new Mock<IFinanzOnlineMetrics>().Object,
            Card(intentId, payment.Id, onLink: () => cardLinked = true),
            (_, _, _, _, _, _, _, _, _, _) => throw new InvalidOperationException("audit down"),
            (_, _) => Task.FromResult(("qr-payload", true, "Demo")));

        var result = await sut.DispatchPostCommitAsync(
            request, "cashier", new Customer { Name = "Ada" }, Guid.NewGuid(),
            payment, invoice, Array.Empty<IssuedTicketDto>(), 0, true, null);

        Assert.True(result.Success);
        Assert.True(result.IsDemoFiscal);
        Assert.True(cardLinked);
        fon.Verify(service => service.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_WhenPrescriptionAuditThrows_SwallowsAndStillLinksCard()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = Payment();
        var invoice = new Invoice { Id = Guid.NewGuid() };
        var intentId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(
            Guid.NewGuid(), Guid.NewGuid(), payment.CashRegisterId, cardPaymentIntentId: intentId);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(service => service.LogSystemOperationAsync(
                It.Is<string>(action => action == "PAYMENT_WITH_PRESCRIPTION"),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));
        var cardLinked = false;
        var sut = Create(
            ctx,
            audit.Object,
            new Mock<IFinanzOnlineService>().Object,
            new Mock<IPaymentFinanzOnlineSubmissionService>().Object,
            null,
            Card(intentId, payment.Id, onLink: () => cardLinked = true),
            (_, _, _, _, _, _, _, _, _, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(("qr-payload", false, "Device")));

        var result = await sut.DispatchPostCommitAsync(
            request, "cashier", new Customer { Name = "Ada" }, Guid.NewGuid(),
            payment, invoice, Array.Empty<IssuedTicketDto>(), 0, false, null);

        Assert.True(result.Success);
        Assert.True(cardLinked);
    }

    [Fact]
    public async Task Dispatch_WhenTseNotRequired_SkipsFinanzOnline()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = new PaymentDetails
        {
            TotalAmount = 5m,
            PaymentMethodRaw = "cash",
            ReceiptNumber = "R-2",
            CashRegisterId = Guid.NewGuid()
        };
        var invoice = new Invoice();
        var audited = false;
        var fon = new Mock<IFinanzOnlineService>();
        var sut = Create(
            ctx,
            Audit(new List<string>()),
            fon.Object,
            new Mock<IPaymentFinanzOnlineSubmissionService>().Object,
            new Mock<IFinanzOnlineMetrics>().Object,
            card: null,
            (action, _, _, _, _, _, _, _, _, _) =>
            {
                audited = action == "PaymentCreated";
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(("qr", false, "None")));

        var result = await sut.DispatchPostCommitAsync(
            PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), payment.CashRegisterId),
            "cashier",
            new Customer { Name = "Ada" },
            Guid.NewGuid(),
            payment,
            invoice,
            Array.Empty<IssuedTicketDto>(),
            0,
            effectiveTseRequired: false,
            offlineReplayBatchCorrelationId: null);

        Assert.True(result.Success);
        Assert.True(audited);
        fon.Verify(service => service.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Never);
    }

    private static PaymentDetails Payment() => new()
    {
        TotalAmount = 12m,
        PaymentMethodRaw = "card",
        TseSignature = "sig",
        ReceiptNumber = "R-1",
        PrescriptionReference = "RX-1",
        TimeSyncWarning = true,
        CashRegisterId = Guid.NewGuid()
    };

    private static IAuditLogService Audit(List<string> steps, string? throwOnAction = null)
    {
        var proxy = DispatchProxy.Create<IAuditLogService, RecordingAudit>();
        var recording = (RecordingAudit)(object)proxy;
        recording.Steps = steps;
        recording.ThrowOnAction = throwOnAction;
        return proxy;
    }

    private class RecordingAudit : DispatchProxy
    {
        public List<string> Steps { get; set; } = new();
        public string? ThrowOnAction { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IAuditLogService.LogSystemOperationAsync))
                throw new NotSupportedException(targetMethod?.Name);

            var action = (string)args![0]!;
            Steps.Add(action);
            if (ThrowOnAction == action)
                throw new InvalidOperationException("audit down");
            return Task.FromResult(new AuditLog());
        }
    }

    private static IFinanzOnlineService Finanz(Invoice invoice, FinanzOnlineSubmitResponse result, List<string> steps)
    {
        var fon = new Mock<IFinanzOnlineService>();
        fon.Setup(service => service.SubmitInvoiceAsync(invoice))
            .Callback(() => steps.Add("fon"))
            .ReturnsAsync(result);
        return fon.Object;
    }

    private static ICardPaymentService Card(Guid intentId, Guid paymentId, List<string>? steps = null, Action? onLink = null)
    {
        var card = new Mock<ICardPaymentService>();
        card.Setup(service => service.LinkToPaymentAsync(intentId, paymentId, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                steps?.Add("card");
                onLink?.Invoke();
            })
            .Returns(Task.CompletedTask);
        return card.Object;
    }

    private static PaymentPostCommitService Create(
        AppDbContext context,
        IAuditLogService audit,
        IFinanzOnlineService finanzOnline,
        IPaymentFinanzOnlineSubmissionService submission,
        IFinanzOnlineMetrics? metrics,
        ICardPaymentService? card,
        PaymentAuditWriter logPaymentAudit,
        PaymentQrPayloadBuilder buildQr) =>
        new(
            context,
            NullLogger<PaymentService>.Instance,
            audit,
            finanzOnline,
            submission,
            new InventoryOptions { EnforceStockOnSales = true },
            logPaymentAudit,
            buildQr,
            metrics,
            card);
}
