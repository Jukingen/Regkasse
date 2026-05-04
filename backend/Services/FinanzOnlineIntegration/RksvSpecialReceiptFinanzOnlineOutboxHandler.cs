using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Processes FinanzOnline outbox rows for RKSV Startbeleg/Jahresbeleg using <see cref="IRksvFinanzOnlineSubmissionClient"/> (fake by default; no BMF network).
/// </summary>
public sealed class RksvSpecialReceiptFinanzOnlineOutboxHandler
{
    private const int MaxRawResponseSnapshotChars = 16000;

    private readonly IRksvFinanzOnlineSubmissionClient _submissionClient;
    private readonly ILogger<RksvSpecialReceiptFinanzOnlineOutboxHandler> _logger;

    public RksvSpecialReceiptFinanzOnlineOutboxHandler(
        IRksvFinanzOnlineSubmissionClient submissionClient,
        ILogger<RksvSpecialReceiptFinanzOnlineOutboxHandler> logger)
    {
        _submissionClient = submissionClient;
        _logger = logger;
    }

    public async Task ProcessAsync(
        AppDbContext context,
        IAuditLogService audit,
        FinanzOnlineOutboxMessage active,
        FinanzOnlineOutboxPayload outerPayload,
        FinanzOnlineOutboxOptions outboxOpts,
        bool isJahresbeleg,
        CancellationToken cancellationToken)
    {
        RksvSpecialReceiptFinanzOnlineOutboxPayloadBody? inner;
        try
        {
            inner = JsonSerializer.Deserialize<RksvSpecialReceiptFinanzOnlineOutboxPayloadBody>(
                outerPayload.PayloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "RKSV FO outbox inner payload JSON invalid OutboxId={OutboxId}", active.Id);
            await MarkPermanentFailureAsync(context, active, "RKS_MALFORMED_INNER_PAYLOAD", "Cannot parse RKSV inner outbox payload.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (inner == null || inner.PaymentId == Guid.Empty)
        {
            await MarkPermanentFailureAsync(context, active, "RKS_INNER_PAYLOAD_MISSING", "RKSV inner outbox payload is empty or missing payment id.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var submission = await context.RksvSpecialReceiptFinanzOnlineSubmissions
            .FirstOrDefaultAsync(s => s.PaymentId == inner.PaymentId, cancellationToken)
            .ConfigureAwait(false);
        if (submission == null)
        {
            await MarkPermanentFailureAsync(
                context,
                active,
                "RKS_SUBMISSION_ROW_MISSING",
                "No rksv_special_receipt_finanz_online_submissions row for payment.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(submission.Status, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, StringComparison.Ordinal))
        {
            await CompleteOutboxIdempotentVerifiedAsync(context, audit, active, inner, cancellationToken).ConfigureAwait(false);
            return;
        }

        var payment = await context.PaymentDetails.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == inner.PaymentId, cancellationToken)
            .ConfigureAwait(false);
        if (payment == null)
        {
            var missingPayNow = DateTime.UtcNow;
            submission.AttemptCount += 1;
            submission.LastAttemptAtUtc = missingPayNow;
            submission.LastErrorCode = "RKS_PAYMENT_NOT_FOUND";
            submission.LastErrorMessage = "Payment row missing for RKSV FinanzOnline submission.";
            submission.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Failed;
            submission.UpdatedAtUtc = missingPayNow;
            active.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
            active.LastErrorCode = "RKS_PAYMENT_NOT_FOUND";
            active.LastErrorMessage = "Payment row missing for RKSV FinanzOnline submission.";
            active.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            active.ProcessedAt = missingPayNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await LogOutboxAttemptAsync(audit, active, "rksv_special_receipt_payment_missing", cancellationToken).ConfigureAwait(false);
            return;
        }

        Receipt? receipt = null;
        if (inner.ReceiptId != Guid.Empty)
        {
            receipt = await context.Receipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReceiptId == inner.ReceiptId, cancellationToken)
                .ConfigureAwait(false);
        }

        receipt ??= await context.Receipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PaymentId == inner.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        var qrPayload = !string.IsNullOrEmpty(receipt?.QrCodePayload)
            ? receipt.QrCodePayload!
            : inner.QrPayload ?? string.Empty;
        var receiptNumber = !string.IsNullOrWhiteSpace(receipt?.ReceiptNumber)
            ? receipt.ReceiptNumber
            : inner.ReceiptNumber;

        var registerNumber = outerPayload.Scope.RegisterId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(registerNumber))
            registerNumber = inner.ReceiptNumber.Length > 0 ? inner.ReceiptNumber : "UNKNOWN";

        var submitPayload = new RksvFinanzOnlineSubmissionPayload
        {
            TenantId = outerPayload.Scope.TenantId,
            CompanyTaxNumber = payment.Steuernummer,
            CashRegisterId = inner.CashRegisterId,
            RegisterNumber = registerNumber,
            ReceiptNumber = receiptNumber,
            QrPayload = qrPayload,
            CertificateSerial = null,
            TimestampUtc = DateTimeOffset.UtcNow,
        };

        RksvFinanzOnlineSubmissionResult result;
        try
        {
            result = isJahresbeleg
                ? await _submissionClient.SubmitJahresbelegAsync(submitPayload, cancellationToken).ConfigureAwait(false)
                : await _submissionClient.SubmitStartbelegAsync(submitPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogWarning(ex, "RKSV FinanzOnline submission client not implemented OutboxId={OutboxId}", active.Id);
            var terminalNow = DateTime.UtcNow;
            active.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
            active.LastErrorCode = "RKS_CLIENT_NOT_IMPLEMENTED";
            active.LastErrorMessage = Truncate(
                "RKSV FinanzOnline submission client is not implemented; configure FinanzOnline:RksvSubmission:ClientKind=Fake for non-production.",
                500);
            active.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            active.ProcessedAt = terminalNow;
            submission.AttemptCount += 1;
            submission.LastAttemptAtUtc = terminalNow;
            submission.LastErrorCode = "RKS_CLIENT_NOT_IMPLEMENTED";
            submission.LastErrorMessage = Truncate(ex.Message, 500);
            submission.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Failed;
            submission.UpdatedAtUtc = terminalNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await LogOutboxAttemptAsync(audit, active, "rksv_special_receipt_client_not_implemented", cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = DateTime.UtcNow;
        submission.LastAttemptAtUtc = now;
        submission.UpdatedAtUtc = now;

        if (result.Success)
        {
            submission.LastErrorCode = null;
            submission.LastErrorMessage = null;
            submission.ExternalReference = Truncate(result.ExternalReference, 120);
            submission.RawResponseSnapshot = Truncate(result.RawResponseSnapshot, MaxRawResponseSnapshotChars);

            var vs = result.VerificationStatus?.Trim();
            if (string.Equals(vs, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, StringComparison.OrdinalIgnoreCase))
            {
                submission.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified;
                submission.VerifiedAtUtc = now;
                submission.SubmittedAtUtc ??= now;
            }
            else
            {
                submission.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Submitted;
                submission.SubmittedAtUtc ??= now;
            }

            active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
            active.FailureCategory = null;
            active.LastErrorCode = null;
            active.LastErrorMessage = null;
            active.ExternalReferenceId = Truncate(result.ExternalReference, 120);
            active.ExternalStatus = Truncate(result.VerificationStatus, 40);
            active.TransmissionId = Truncate($"RKS-TX-{Guid.NewGuid():N}", 120);
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            active.ProcessedAt = now;
            active.LastResponseJson = JsonSerializer.Serialize(new
            {
                result = "RksvSpecialReceiptSubmissionSuccess",
                paymentId = inner.PaymentId,
                receiptId = inner.ReceiptId,
                success = true,
                externalReference = result.ExternalReference,
                verificationStatus = result.VerificationStatus,
            });
            active.ProtocolCode = null;
            active.ProtocolSummary = null;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "FinanzOnline outbox RKSV special receipt submission success OutboxId={OutboxId} PaymentId={PaymentId} SubmissionStatus={SubmissionStatus}",
                active.Id,
                inner.PaymentId,
                submission.Status);
            await LogOutboxAttemptAsync(audit, active, "rksv_special_receipt_submission_success", cancellationToken).ConfigureAwait(false);
            return;
        }

        submission.AttemptCount += 1;
        submission.LastErrorCode = Truncate(result.ErrorCode, 80);
        submission.LastErrorMessage = Truncate(result.ErrorMessage, 500);

        var classified = ClassifyRksvClientFailure(result.ErrorCode);
        var retryable = classified.retryable && active.AttemptCount < outboxOpts.MaxAttempts;
        if (retryable)
        {
            var delay = ComputeBackoffSecondsWithJitter(active.Id, active.AttemptCount, outboxOpts.BaseDelaySeconds, outboxOpts.BackoffCapSeconds, outboxOpts.JitterMaxSeconds);
            active.Status = FinanzOnlineOutboxStatuses.RetryableFailure;
            active.NextAttemptAt = DateTime.UtcNow.AddSeconds(delay);
            active.FailureCategory = FinanzOnlineFailureCategories.RetryableTransient;
            submission.Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending;
        }
        else
        {
            active.Status = active.AttemptCount >= outboxOpts.MaxAttempts ? FinanzOnlineOutboxStatuses.DeadLetter : classified.terminalStatus;
            active.FailureCategory = active.Status == FinanzOnlineOutboxStatuses.DeadLetter
                ? "MaxAttemptsExceeded"
                : classified.category;
            active.ProcessedAt = DateTime.UtcNow;
            submission.Status = ShouldMarkManualVerificationForTerminalError(result.ErrorCode)
                ? RksvSpecialReceiptFinanzOnlineSubmissionStatuses.ManualVerificationRequired
                : RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Failed;
        }

        active.LastErrorCode = Truncate(result.ErrorCode, 80);
        active.LastErrorMessage = Truncate(result.ErrorMessage, 500);
        active.ExternalStatus = Truncate(result.VerificationStatus, 40);
        active.LastResponseJson = JsonSerializer.Serialize(new
        {
            result = "RksvSpecialReceiptSubmissionFailure",
            paymentId = inner.PaymentId,
            receiptId = inner.ReceiptId,
            success = false,
            result.ErrorCode,
            result.ErrorMessage,
        });
        active.ProcessingToken = null;
        active.ProcessingStartedAt = null;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning(
            "FinanzOnline outbox RKSV special receipt submission failed OutboxId={OutboxId} PaymentId={PaymentId} OutboxStatus={OutboxStatus} Attempt={Attempt}",
            active.Id,
            inner.PaymentId,
            active.Status,
            active.AttemptCount);
        await LogOutboxAttemptAsync(audit, active, "rksv_special_receipt_submission_failure", cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteOutboxIdempotentVerifiedAsync(
        AppDbContext context,
        IAuditLogService audit,
        FinanzOnlineOutboxMessage active,
        RksvSpecialReceiptFinanzOnlineOutboxPayloadBody inner,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
        active.FailureCategory = null;
        active.LastErrorCode = null;
        active.LastErrorMessage = null;
        active.ProcessingToken = null;
        active.ProcessingStartedAt = null;
        active.ProcessedAt = now;
        active.LastResponseJson = JsonSerializer.Serialize(new
        {
            result = "RksvSpecialReceiptIdempotentSkip",
            reason = "AlreadyVerified",
            paymentId = inner.PaymentId,
            receiptId = inner.ReceiptId,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "FinanzOnline outbox RKSV special receipt skipped (already verified) OutboxId={OutboxId} PaymentId={PaymentId}",
            active.Id,
            inner.PaymentId);
        await LogOutboxAttemptAsync(audit, active, "rksv_special_receipt_idempotent_verified", cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldMarkManualVerificationForTerminalError(string? errorCode) =>
        string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled, StringComparison.OrdinalIgnoreCase);

    private static (bool retryable, string category, string terminalStatus) ClassifyRksvClientFailure(string? errorCode)
    {
        if (string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.SoapTransportNotImplemented, StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.PermanentBusiness, FinanzOnlineOutboxStatuses.PermanentFailure);

        if (!string.IsNullOrWhiteSpace(errorCode) && errorCode.StartsWith("FAKE_", StringComparison.OrdinalIgnoreCase))
            return (true, FinanzOnlineFailureCategories.RetryableTransient, FinanzOnlineOutboxStatuses.PermanentFailure);

        if (string.IsNullOrWhiteSpace(errorCode))
            return (true, FinanzOnlineFailureCategories.RetryableTransient, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("HTTP_5", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("HTTP_429", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
            return (true, FinanzOnlineFailureCategories.RetryableTransient, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("SESSION", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.Session, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.Authorization, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("RKDB_XML_STRUCTURE_INVALID", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_COMMAND_INVALID", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_MODE_NOT_SUPPORTED", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_XML_PAYLOAD_REQUIRED", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.PermanentBusiness, FinanzOnlineOutboxStatuses.PermanentFailure);
        return (false, FinanzOnlineFailureCategories.PermanentBusiness, FinanzOnlineOutboxStatuses.PermanentFailure);
    }

    private static int ComputeBackoffSecondsWithJitter(Guid messageId, int attempt, int baseDelaySeconds, int capSeconds, int jitterMaxSeconds)
    {
        var delay = baseDelaySeconds * (int)Math.Pow(2, Math.Min(attempt, 20));
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{messageId:N}:{attempt}"));
        var seed = BitConverter.ToUInt32(seedBytes, 0);
        var jitter = jitterMaxSeconds <= 0 ? 0 : (int)(seed % (uint)(jitterMaxSeconds + 1));
        return Math.Min(delay + jitter, capSeconds);
    }

    private static async Task MarkPermanentFailureAsync(
        AppDbContext context,
        FinanzOnlineOutboxMessage item,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        item.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
        item.LastErrorCode = code;
        item.LastErrorMessage = message;
        item.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
        item.ProcessingToken = null;
        item.ProcessingStartedAt = null;
        item.ProcessedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogOutboxAttemptAsync(
        IAuditLogService audit,
        FinanzOnlineOutboxMessage message,
        string eventName,
        CancellationToken cancellationToken)
    {
        await audit.LogSystemOperationAsync(
            $"FinanzOnlineOutboxAttempt.{eventName}",
            nameof(FinanzOnlineOutboxMessage),
            "system",
            "OutboxWorker",
            description: "FinanzOnline outbox attempt persisted",
            requestData: new
            {
                outboxId = message.Id,
                message.AggregateType,
                message.AggregateId,
                message.MessageType,
                message.CorrelationId,
                message.AttemptCount,
            },
            responseData: new
            {
                message.Status,
                message.FailureCategory,
                message.LastErrorCode,
                message.LastErrorMessage,
                message.TransmissionId,
                message.ExternalReferenceId,
                message.ProtocolCode,
            },
            status: AuditLogStatus.Success);
    }

    private static string? Truncate(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
    }
}
