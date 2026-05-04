using System.Text.Json;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvSpecialReceiptFinanzOnlineOutboxHandlerTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RksvFonHandler_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<RksvSpecialReceiptFinanzOnlineOutboxScenario> SeedMinimalScenarioAsync(AppDbContext db)
    {
        TenantTestDoubles.EnsureDefaultTenant(db);
        db.Customers.Add(new Customer
        {
            Id = WalkInCustomerConstants.GuestCustomerId,
            Name = "Gast",
            Email = "gast@test",
            Phone = "0",
            Address = "",
            TaxNumber = "",
            CustomerNumber = "",
            IsActive = true,
        });
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = LegacyDefaultTenantIds.Primary,
            RegisterNumber = "REG-99",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        var paymentId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "system",
            TotalAmount = 0,
            TaxAmount = 0,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "AT-REG-20260101-9",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Startbeleg,
            CreatedAt = DateTime.UtcNow,
        });
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            PaymentId = paymentId,
            ReceiptNumber = "AT-REG-20260101-9",
            IssuedAt = DateTime.UtcNow,
            CashRegisterId = regId,
            SubTotal = 0,
            TaxTotal = 0,
            GrandTotal = 0,
            QrCodePayload = "QR-FROM-DB",
            CreatedAt = DateTime.UtcNow,
        });
        var submission = new RksvSpecialReceiptFinanzOnlineSubmission
        {
            PaymentId = paymentId,
            ReceiptId = receiptId,
            CashRegisterId = regId,
            Kind = RksvSpecialReceiptKinds.Startbeleg,
            Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        db.RksvSpecialReceiptFinanzOnlineSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var inner = new RksvSpecialReceiptFinanzOnlineOutboxPayloadBody
        {
            Kind = RksvSpecialReceiptKinds.Startbeleg,
            PaymentId = paymentId,
            ReceiptId = receiptId,
            CashRegisterId = regId,
            ReceiptNumber = "AT-REG-20260101-9",
            QrPayload = "QR-FROM-OUTBOX",
        };
        var outer = new FinanzOnlineOutboxPayload
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { TenantId = "tenant-x", RegisterId = "REG-99" },
            Correlation = new FinanzOnlineCorrelationContext { CorrelationId = paymentId.ToString("N") },
            PayloadJson = JsonSerializer.Serialize(inner, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
        var outbox = new FinanzOnlineOutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateType = "RksvSpecialReceipt",
            AggregateId = receiptId,
            MessageType = FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission,
            BusinessKey = "bk",
            IdempotencyKey = "idem-" + Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(outer),
            PayloadHash = new string('a', 64),
            Mode = "TEST",
            Status = FinanzOnlineOutboxStatuses.Processing,
            AttemptCount = 1,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = paymentId.ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ProcessingToken = "claim",
            ProcessingStartedAt = DateTime.UtcNow,
        };
        db.FinanzOnlineOutboxMessages.Add(outbox);
        await db.SaveChangesAsync();
        return new RksvSpecialReceiptFinanzOnlineOutboxScenario(db, outbox, outer, paymentId, receiptId);
    }

    private sealed record RksvSpecialReceiptFinanzOnlineOutboxScenario(
        AppDbContext Db,
        FinanzOnlineOutboxMessage Outbox,
        FinanzOnlineOutboxPayload Outer,
        Guid PaymentId,
        Guid ReceiptId);

    [Fact]
    public async Task Success_sets_submission_verified_and_outbox_protocol_success()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var client = new Mock<IRksvFinanzOnlineSubmissionClient>();
        client.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvFinanzOnlineSubmissionResult
            {
                Success = true,
                ExternalReference = "EXT-OK",
                VerificationStatus = "Verified",
                RawResponseSnapshot = """{"ok":true}""",
            });
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };

        var active = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == scenario.PaymentId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, sub.Status);
        Assert.Equal("EXT-OK", sub.ExternalReference);
        Assert.NotNull(sub.VerifiedAtUtc);
        var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == scenario.Outbox.Id);
        Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
        Assert.Equal("EXT-OK", after.ExternalReferenceId);
        client.Verify(x => x.SubmitStartbelegAsync(
            It.Is<RksvFinanzOnlineSubmissionPayload>(p => p.QrPayload == "QR-FROM-DB"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failure_sets_retryable_outbox_and_increments_submission_attempts()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var client = new Mock<IRksvFinanzOnlineSubmissionClient>();
        client.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = "FAKE_RKSV_SUBMISSION_FAILED",
                ErrorMessage = "No",
                VerificationStatus = "Rejected",
            });
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };

        var active = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == scenario.PaymentId);
        Assert.Equal(1, sub.AttemptCount);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending, sub.Status);
        Assert.Equal("FAKE_RKSV_SUBMISSION_FAILED", sub.LastErrorCode);
        var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == scenario.Outbox.Id);
        Assert.Equal(FinanzOnlineOutboxStatuses.RetryableFailure, after.Status);
    }

    [Fact]
    public async Task Retry_after_failure_eventually_verifies()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var client = new Mock<IRksvFinanzOnlineSubmissionClient>();
        var calls = 0;
        client.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return calls == 1
                    ? new RksvFinanzOnlineSubmissionResult { Success = false, ErrorCode = "FAKE_ERR", ErrorMessage = "x" }
                    : new RksvFinanzOnlineSubmissionResult { Success = true, ExternalReference = "EXT-2", VerificationStatus = "Verified" };
            });
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };

        var active1 = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active1, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);
        Assert.Equal(FinanzOnlineOutboxStatuses.RetryableFailure, active1.Status);

        var active2 = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        active2.AttemptCount = 2;
        await handler.ProcessAsync(db, audit.Object, active2, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == scenario.PaymentId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, sub.Status);
        Assert.Equal(1, sub.AttemptCount);
        var afterOb = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == scenario.Outbox.Id);
        Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, afterOb.Status);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Success_with_pending_verification_marks_submitted_not_verified()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var client = new Mock<IRksvFinanzOnlineSubmissionClient>();
        client.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvFinanzOnlineSubmissionResult
            {
                Success = true,
                ExternalReference = "EXT-PEND",
                VerificationStatus = "Pending",
            });
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };
        var active = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == scenario.PaymentId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Submitted, sub.Status);
        Assert.NotNull(sub.SubmittedAtUtc);
        Assert.Null(sub.VerifiedAtUtc);
    }

    [Fact]
    public async Task Submission_disabled_error_sets_manual_verification_on_terminal_outcome()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var client = new Mock<IRksvFinanzOnlineSubmissionClient>();
        client.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled,
                ErrorMessage = "disabled",
                VerificationStatus = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.ManualVerificationRequired,
            });
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };

        var active = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == scenario.PaymentId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.ManualVerificationRequired, sub.Status);
        Assert.Equal(FinanzOnlineOutboxStatuses.PermanentFailure, (await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == scenario.Outbox.Id)).Status);
    }

    [Fact]
    public async Task Already_verified_skips_client_and_completes_outbox()
    {
        await using var db = CreateInMemoryContext();
        var scenario = await SeedMinimalScenarioAsync(db);
        var subRow = await db.RksvSpecialReceiptFinanzOnlineSubmissions.FirstAsync(s => s.PaymentId == scenario.PaymentId);
        subRow.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified;
        subRow.VerifiedAtUtc = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var client = new Mock<IRksvFinanzOnlineSubmissionClient>(MockBehavior.Strict);
        var handler = new RksvSpecialReceiptFinanzOnlineOutboxHandler(client.Object, NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        var opts = new FinanzOnlineOutboxOptions { MaxAttempts = 8, BaseDelaySeconds = 1, BackoffCapSeconds = 60, JitterMaxSeconds = 0 };

        var active = await db.FinanzOnlineOutboxMessages.FirstAsync(x => x.Id == scenario.Outbox.Id);
        await handler.ProcessAsync(db, audit.Object, active, scenario.Outer, opts, isJahresbeleg: false, CancellationToken.None);

        client.Verify(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()), Times.Never());
        var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == scenario.Outbox.Id);
        Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
    }
}
