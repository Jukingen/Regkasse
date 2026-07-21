using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public class OfflineTransactionService : IOfflineTransactionService
{
    private const int MaxRetryLimit = 5;
    private static readonly TimeSpan ClockDriftTolerance = TimeSpan.FromMinutes(5);
    private const int BackoffBaseSeconds = 10;
    private const int BackoffMaxSeconds = 300;

    /// <summary>
    /// Scan recent rows per register and match incoming replay hash to SHA256(runtime-canonical(PayloadJson)).
    /// Covers legacy DB rows where payload_hash was backfilled as digest(PayloadJson::text) without key ordering.
    /// </summary>
    private const int RuntimeRecomputedHashCandidateLimit = 2000;

    private readonly AppDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<OfflineTransactionService> _logger;
    private readonly OfflineReplayOptions _replayOptions;
    private readonly ICoreMetrics? _metrics;
    private readonly IDataProtector _offlineFullPayloadProtector;
    private readonly IOptionsMonitor<OfflineVoucherEncryptionOptions>? _offlineVoucherEncryption;

    public OfflineTransactionService(
        AppDbContext context,
        IPaymentService paymentService,
        IAuditLogService auditLogService,
        ILogger<OfflineTransactionService> logger,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<OfflineReplayOptions>? replayOptions = null,
        ICoreMetrics? metrics = null,
        IOptionsMonitor<OfflineVoucherEncryptionOptions>? offlineVoucherEncryption = null)
    {
        _context = context;
        _paymentService = paymentService;
        _auditLogService = auditLogService;
        _logger = logger;
        _replayOptions = replayOptions?.Value ?? new OfflineReplayOptions();
        _metrics = metrics;
        _offlineFullPayloadProtector = OfflineVoucherPayloadProtector.CreateProtector(dataProtectionProvider);
        _offlineVoucherEncryption = offlineVoucherEncryption;
    }

    /// <summary>Optional AES layer for voucher-bearing offline payloads (before Data Protection seal).</summary>
    private byte[]? GetOfflineVoucherFieldAesKeyBytes() =>
        OfflineVoucherEncryptionOptions.TryResolveKeyBytes(_offlineVoucherEncryption?.CurrentValue);

    public async Task<ReplayOfflineTransactionsResponse> ReplayOfflineTransactionsAsync(
        ReplayOfflineTransactionsRequest request,
        string userId,
        string userRole)
    {
        if (request == null || request.Transactions == null || request.Transactions.Count == 0)
            return new ReplayOfflineTransactionsResponse { ReplayBatchCorrelationId = null };

        var replayBatchCorrelationId = Guid.NewGuid();
        var replayBatchAuditKey = replayBatchCorrelationId.ToString("N");

        // Replay order must be kept as provided by the client.
        var ordered = request.Transactions.ToList();
        var results = new List<ReplayOfflineTransactionsResponseItem>(ordered.Count);

        // Serialize per-register replay across API instances (PostgreSQL only).
        var provider = _context.Database.ProviderName ?? string.Empty;
        var isPostgres = provider.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0;
        string? connectionString = null;
        if (isPostgres && _context.Database.IsRelational())
            connectionString = _context.Database.GetConnectionString();

        OfflineReplayRegisterLock.OfflineReplayRegisterLockScope? lockScope = null;
        if (isPostgres && _context.Database.IsRelational() && !string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                lockScope = await OfflineReplayRegisterLock.AcquireAsync(
                    ordered.Select(x => x.CashRegisterId),
                    connectionString!,
                    CancellationToken.None,
                    _replayOptions.MaxLockWaitMs,
                    _replayOptions.LockRetryIntervalMs).ConfigureAwait(false);
                if (lockScope.WaitDurationMs > 0)
                    _logger.LogInformation(
                        "Offline replay advisory lock acquired after {WaitDurationMs}ms. ReplayBatchCorrelationId={ReplayBatchCorrelationId}",
                        lockScope.WaitDurationMs, replayBatchCorrelationId);
                _metrics?.RecordAdvisoryLockWaitSeconds(lockScope.WaitDurationMs / 1000.0);
            }
            catch (OfflineReplayLockTimeoutException ex)
            {
                _logger.LogWarning(ex,
                    "Offline replay advisory lock timeout after {WaitDurationMs}ms for register(s) {RegisterIds}. ReplayBatchCorrelationId={ReplayBatchCorrelationId}",
                    ex.WaitDurationMs, string.Join(", ", ex.CashRegisterIds), replayBatchCorrelationId);
                await _auditLogService.LogSystemOperationAsync(
                    "OfflineReplayLockTimeout",
                    "OfflineReplay",
                    userId,
                    userRole,
                    description: $"Advisory lock timeout after {ex.WaitDurationMs}ms; registers: {string.Join(", ", ex.CashRegisterIds)}",
                    status: AuditLogStatus.Failed,
                    errorDetails: ex.Message,
                    requestData: new { ex.WaitDurationMs, RegisterIds = ex.CashRegisterIds, ReplayBatchCorrelationId = replayBatchCorrelationId },
                    correlationIdOverride: replayBatchAuditKey).ConfigureAwait(false);
                foreach (var item in ordered)
                {
                    _metrics?.RecordReplayTotal(1);
                    _metrics?.RecordReplayFailed(1);
                    results.Add(FailedLocalItem(item.OfflineTransactionId, item.OfflineTransactionId, "LOCK_TIMEOUT", "Advisory lock timeout; try again later.", 0, replayBatchCorrelationId));
                }
                return new ReplayOfflineTransactionsResponse { ReplayBatchCorrelationId = replayBatchCorrelationId, Items = results };
            }
        }

        try
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["ReplayBatchCorrelationId"] = replayBatchCorrelationId,
                ["ReplayBatchItemCount"] = ordered.Count
            }))
            {
                _logger.LogInformation(
                    "Offline replay batch started: ReplayBatchCorrelationId={ReplayBatchCorrelationId}, itemCount={ItemCount}",
                    replayBatchCorrelationId,
                    ordered.Count);

                foreach (var item in ordered)
                {
                    _metrics?.RecordReplayTotal(1);
                    var payloadRaw = GetPayloadRaw(item.Payload);
                    var payloadPrepared = OfflineVoucherPayloadProtector.PrepareForPersistence(
                        payloadRaw,
                        _offlineFullPayloadProtector,
                        GetOfflineVoucherFieldAesKeyBytes());
                    var normalizedPayloadJson = payloadPrepared.StoredPayloadJson;
                    var payloadHash = payloadPrepared.PayloadHashHex;

                    if (item.OfflineTransactionId == Guid.Empty)
                    {
                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedLocalItem(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: item.OfflineTransactionId,
                            errorCode: "INVALID_ID",
                            message: "OfflineTransactionId must not be empty.",
                            retryCount: 0,
                            replayBatchCorrelationId));
                        continue;
                    }

                    if (item.CashRegisterId == Guid.Empty)
                    {
                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedLocalItem(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: item.OfflineTransactionId,
                            errorCode: "INVALID_CASH_REGISTER",
                            message: "CashRegisterId must not be empty.",
                            retryCount: 0,
                            replayBatchCorrelationId));
                        continue;
                    }

                    await RecordOfflineIntentCoverageAsync(item.CashRegisterId, item.DeviceId, item.ClientSequenceNumber, replayBatchCorrelationId).ConfigureAwait(false);

                    OfflineTransaction? offline = null;
                    var createdThisCall = false;

                    // 1) Resolve OfflineTransaction (requested id first).
                    string? replayPath = null;
                    offline = await _context.OfflineTransactions
                        .FirstOrDefaultAsync(x => x.Id == item.OfflineTransactionId)
                        .ConfigureAwait(false);
                    if (offline != null)
                        replayPath = "requested_id";

                    if (offline == null)
                    {
                        // 2) Payload hash deduplication: resolve by (CashRegisterId, PayloadHash).
                        offline = await _context.OfflineTransactions
                            .FirstOrDefaultAsync(x =>
                                x.CashRegisterId == item.CashRegisterId &&
                                x.PayloadHash == payloadHash)
                            .ConfigureAwait(false);

                        if (offline != null)
                        {
                            replayPath = "hash_match";
                            if (offline.Id != item.OfflineTransactionId)
                            {
                                await TryWriteOfflinePayloadDedupAuditAsync(
                                    offline,
                                    requestedOfflineId: item.OfflineTransactionId,
                                    userId,
                                    userRole,
                                    replayBatchCorrelationId,
                                    replayBatchAuditKey,
                                    replayPath).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            // 3) Deterministic: same register, recompute runtime hash from stored PayloadJson.
                            offline = await TryResolveOfflineByRuntimeRecomputedHashAsync(
                                    item.CashRegisterId,
                                    payloadHash)
                                .ConfigureAwait(false);

                            if (offline != null)
                            {
                                replayPath = "recompute";
                                if (offline.Id != item.OfflineTransactionId)
                                {
                                    await TryWriteOfflinePayloadDedupAuditAsync(
                                            offline,
                                            requestedOfflineId: item.OfflineTransactionId,
                                            userId,
                                            userRole,
                                            replayBatchCorrelationId,
                                            replayBatchAuditKey,
                                            replayPath).ConfigureAwait(false);
                                }
                            }

                            if (offline == null && _replayOptions.AllowStructuralFallback)
                            {
                                // 4) Last-resort structural match (narrow window; kill-switch via OfflineReplay:AllowStructuralFallback).
                                offline = await TryResolveOfflineByStructuralPayloadAsync(
                                        item.CashRegisterId,
                                        normalizedPayloadJson)
                                    .ConfigureAwait(false);

                                if (offline != null)
                                {
                                    replayPath = "structural";
                                    if (offline.Id != item.OfflineTransactionId)
                                    {
                                        await TryWriteOfflinePayloadDedupAuditAsync(
                                                offline,
                                                requestedOfflineId: item.OfflineTransactionId,
                                                userId,
                                                userRole,
                                                replayBatchCorrelationId,
                                                replayBatchAuditKey,
                                                replayPath)
                                            .ConfigureAwait(false);
                                    }
                                }
                            }

                            if (offline == null)
                            {
                                // 5) Create new offline transaction row.
                                offline = await CreateOfflineTransactionRowAsync(
                                        item,
                                        payloadPrepared,
                                        userId,
                                        userRole,
                                        replayBatchCorrelationId,
                                        replayBatchAuditKey)
                                    .ConfigureAwait(false);

                                createdThisCall = true;
                            }
                        }
                    }

                    if (offline == null)
                    {
                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedLocalItem(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: item.OfflineTransactionId,
                            errorCode: "OFFLINE_RESOLVE_FAILED",
                            message: "Could not resolve offline transaction.",
                            retryCount: 0,
                            replayBatchCorrelationId));
                        continue;
                    }

                    // Immutability: content hash covers full voucher-bearing canonical JSON; structural compares stored PayloadJson vs this batch's normalized view (same shape: redacted+DP or legacy plaintext).
                    var hashMatches = string.Equals(payloadHash, offline.PayloadHash, StringComparison.OrdinalIgnoreCase);
                    var structuralMatches =
                        OfflinePayloadComparer.EqualsNormalized(offline.PayloadJson, normalizedPayloadJson);
                    var payloadMatches = hashMatches || structuralMatches;
                    if (!payloadMatches)
                    {
                        // Fraud-resistant: mark offline intent as failed; never replay tampered payload.
                        offline.Status = OfflineTransactionStatus.Failed;
                        offline.LastErrorCode = "PAYLOAD_IMMUTABLE_MISMATCH";
                        offline.LastErrorMessageSafe =
                            "Payload does not match the stored offline intent. Replays are blocked for this offline id.";
                        offline.SyncedPaymentId = null;
                        offline.FiscalizedAtUtc = null;
                        offline.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync().ConfigureAwait(false);

                        await TryWriteOfflineFailedAuditAsync(
                            offline,
                            userId,
                            userRole,
                            actionCode: "PAYLOAD_IMMUTABLE_MISMATCH",
                            replayBatchCorrelationId,
                            replayBatchAuditKey,
                            replayPath: replayPath ?? "unknown",
                            payloadRepaired: false).ConfigureAwait(false);

                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedLocalItem(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            errorCode: offline.LastErrorCode ?? "PAYLOAD_IMMUTABLE_MISMATCH",
                            message: offline.LastErrorMessageSafe ?? "Payload mismatch",
                            retryCount: offline.RetryCount,
                            replayBatchCorrelationId));
                        continue;
                    }

                    var payloadRepaired = await TryAlignStoredPayloadHashToRuntimeCanonicalAsync(offline.Id).ConfigureAwait(false);

                    // If record was resolved by payload hash dedup, we still want to ensure
                    // the requested id doesn't affect fiscalization (PaymentService uses offlineTransactionId).
                    if (createdThisCall)
                    {
                        // Creation audit is done inside CreateOfflineTransactionRowAsync.
                    }

                    // 4) Replay into canonical fiscal payment for Pending only.
                    if (offline.Status == OfflineTransactionStatus.Synced && offline.SyncedPaymentId != null)
                    {
                        _metrics?.RecordReplayDuplicate(1);
                        results.Add(SuccessSyncedItem(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            syncedPaymentId: offline.SyncedPaymentId.Value,
                            retryCount: offline.RetryCount,
                            replayBatchCorrelationId));
                        continue;
                    }

                    if (offline.Status == OfflineTransactionStatus.Failed)
                    {
                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedFromRow(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            row: offline,
                            replayBatchCorrelationId));
                        continue;
                    }

                    // Pending replay path.
                    if (offline.RetryCount >= MaxRetryLimit)
                    {
                        offline.Status = OfflineTransactionStatus.Failed;
                        offline.LastErrorCode = "MAX_RETRY_LIMIT_EXCEEDED";
                        offline.LastErrorMessageSafe = "Offline replay exceeded the maximum retry limit.";
                        offline.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync().ConfigureAwait(false);

                        await TryWriteOfflineFailedAuditAsync(
                            offline,
                            userId,
                            userRole,
                            actionCode: "MAX_RETRY_LIMIT_EXCEEDED",
                            replayBatchCorrelationId,
                            replayBatchAuditKey,
                            replayPath: replayPath ?? "unknown",
                            payloadRepaired).ConfigureAwait(false);

                        _metrics?.RecordReplayFailed(1);
                        results.Add(FailedFromRow(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            row: offline,
                            replayBatchCorrelationId));
                        continue;
                    }

                    offline.LastReplayAttemptAt = DateTime.UtcNow;
                    offline.RetryCount++;
                    offline.LastErrorCode = null;
                    offline.LastErrorMessageSafe = null;
                    offline.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync().ConfigureAwait(false);

                    var attemptUtc = offline.LastReplayAttemptAt.Value;

                    try
                    {
                        var replayJson = OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                            offline.PayloadJson,
                            offline.PayloadSecretsProtected,
                            _offlineFullPayloadProtector,
                            GetOfflineVoucherFieldAesKeyBytes());

                        var paymentRequest =
                            JsonSerializer.Deserialize<CreatePaymentRequest>(replayJson);

                        if (paymentRequest == null)
                            throw new InvalidOperationException("Offline payload could not be deserialized into CreatePaymentRequest.");

                        if (paymentRequest.CashRegisterId != offline.CashRegisterId)
                            throw new InvalidOperationException("Offline payload CashRegisterId does not match the OfflineTransaction CashRegisterId.");

                        // CreatePaymentAsync generates receipt + signature only for the canonical fiscal flow.
                        var paymentResult = await _paymentService.CreatePaymentAsync(
                            paymentRequest,
                            userId,
                            offlineTransactionId: offline.Id,
                            offlineReplayBatchCorrelationId: replayBatchCorrelationId).ConfigureAwait(false);

                        // CreatePaymentAsync may call ChangeTracker.Clear() on fiscal rollback; re-load so offline row updates persist.
                        offline = await _context.OfflineTransactions
                            .FirstAsync(x => x.Id == offline.Id)
                            .ConfigureAwait(false);

                        if (paymentResult.Success && paymentResult.PaymentId.HasValue)
                        {
                            offline.Status = OfflineTransactionStatus.Synced;
                            offline.SyncedPaymentId = paymentResult.PaymentId.Value;
                            offline.FiscalizedAtUtc = DateTime.UtcNow;
                            offline.LastErrorCode = null;
                            offline.LastErrorMessageSafe = null;
                            offline.UpdatedAt = DateTime.UtcNow;

                            await _context.SaveChangesAsync().ConfigureAwait(false);

                            await TryWriteOfflineSyncedAuditAsync(
                                offline,
                                paymentResult.PaymentId.Value,
                                userId,
                                userRole,
                                replayBatchCorrelationId,
                                replayBatchAuditKey,
                                replayPath: replayPath ?? "requested_id",
                                payloadRepaired).ConfigureAwait(false);

                            results.Add(SuccessSyncedItem(
                                requestedId: item.OfflineTransactionId,
                                resolvedId: offline.Id,
                                syncedPaymentId: offline.SyncedPaymentId.Value,
                                retryCount: offline.RetryCount,
                                replayBatchCorrelationId));
                            continue;
                        }

                        var (errCode, safeMsg) = MapPaymentFailure(paymentResult);
                        var isFinalRetry = offline.RetryCount >= MaxRetryLimit || paymentResult.IsDeterministicFailure;

                        offline.LastErrorCode = errCode;
                        offline.LastErrorMessageSafe = safeMsg;
                        var pendingStatus = offline.Status == OfflineTransactionStatus.NonFiscalPending
                            ? OfflineTransactionStatus.NonFiscalPending
                            : OfflineTransactionStatus.Pending;
                        offline.Status = isFinalRetry ? OfflineTransactionStatus.Failed : pendingStatus;
                        offline.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync().ConfigureAwait(false);

                        if (isFinalRetry)
                        {
                            await TryWriteOfflineFailedAuditAsync(
                                offline,
                                userId,
                                userRole,
                                actionCode: "OFFLINE_REPLAY_FAILED_FINAL",
                                replayBatchCorrelationId,
                                replayBatchAuditKey,
                                replayPath: replayPath ?? "unknown",
                                payloadRepaired).ConfigureAwait(false);
                        }

                        if (isFinalRetry)
                            _metrics?.RecordReplayFailed(1);
                        results.Add(PendingOrFailedFromAttempt(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            row: offline,
                            errCode: errCode,
                            safeMsg: safeMsg,
                            nextBackoffHintSeconds: isFinalRetry ? null : ComputeBackoffSeconds(offline.RetryCount),
                            replayBatchCorrelationId));
                    }
                    catch (Exception ex)
                    {
                        offline = await _context.OfflineTransactions
                            .FirstAsync(x => x.Id == offline.Id)
                            .ConfigureAwait(false);

                        _logger.LogWarning(
                            ex,
                            "Offline replay exception for OfflineTransactionId={OfflineId}, ReplayBatchCorrelationId={ReplayBatchCorrelationId}",
                            offline.Id,
                            replayBatchCorrelationId);
                        var safeMsg = SanitizeExceptionMessage(ex);

                        var isFinalRetry = offline.RetryCount >= MaxRetryLimit;
                        offline.LastErrorCode = "REPLAY_EXCEPTION";
                        offline.LastErrorMessageSafe = safeMsg;
                        var pendingAfterException = offline.Status == OfflineTransactionStatus.NonFiscalPending
                            ? OfflineTransactionStatus.NonFiscalPending
                            : OfflineTransactionStatus.Pending;
                        offline.Status = isFinalRetry ? OfflineTransactionStatus.Failed : pendingAfterException;
                        offline.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync().ConfigureAwait(false);

                        if (isFinalRetry)
                        {
                            await TryWriteOfflineFailedAuditAsync(
                                offline,
                                userId,
                                userRole,
                                actionCode: "OFFLINE_REPLAY_EXCEPTION_FINAL",
                                replayBatchCorrelationId,
                                replayBatchAuditKey,
                                replayPath: replayPath ?? "unknown",
                                payloadRepaired).ConfigureAwait(false);
                        }

                        if (isFinalRetry)
                            _metrics?.RecordReplayFailed(1);
                        results.Add(PendingOrFailedFromAttempt(
                            requestedId: item.OfflineTransactionId,
                            resolvedId: offline.Id,
                            row: offline,
                            errCode: offline.LastErrorCode,
                            safeMsg: offline.LastErrorMessageSafe,
                            nextBackoffHintSeconds: isFinalRetry ? null : ComputeBackoffSeconds(offline.RetryCount),
                            replayBatchCorrelationId));
                    }
                }
            }
        }
        finally
        {
            if (lockScope != null)
                await lockScope.DisposeAsync().ConfigureAwait(false);
        }

        return new ReplayOfflineTransactionsResponse
        {
            ReplayBatchCorrelationId = replayBatchCorrelationId,
            Items = results
        };
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
    }

    private async Task<OfflineTransaction> CreateOfflineTransactionRowAsync(
        ReplayOfflineTransactionItem item,
        OfflineVoucherPayloadProtector.PrepareResult prepared,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey)
    {
        var now = DateTime.UtcNow;
        var offlineCreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);

        var offline = new OfflineTransaction
        {
            Id = item.OfflineTransactionId,
            CashRegisterId = item.CashRegisterId,
            ServerReceivedAtUtc = now,
            CreatedAt = now, // BaseEntity.CreatedAt
            OfflineCreatedAtUtc = offlineCreatedAtUtc,
            PayloadJson = prepared.StoredPayloadJson,
            PayloadHash = prepared.PayloadHashHex,
            PayloadSecretsProtected = prepared.ProtectedBase64,
            Status = OfflineTransactionStatus.Pending,
            SyncedPaymentId = null,
            FiscalizedAtUtc = null,
            DeviceId = item.DeviceId,
            ClientSequenceNumber = item.ClientSequenceNumber,
            RetryCount = 0,
            LastReplayAttemptAt = null,
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            LastErrorCode = null,
            LastErrorMessageSafe = null,
            IsActive = true,
            UpdatedAt = now
        };

        // Step 1: clock drift protection (flag-only).
        var drift = offlineCreatedAtUtc - now;
        if (drift > ClockDriftTolerance)
        {
            offline.ClockDriftWarning = true;
            await TryWriteClockDriftAuditAsync(
                offline,
                drift,
                userId,
                userRole,
                replayBatchCorrelationId,
                replayBatchAuditKey).ConfigureAwait(false);
        }

        // Step 2: client sequence tracking (monotonic enforcement).
        if (!string.IsNullOrWhiteSpace(offline.DeviceId) && offline.ClientSequenceNumber.HasValue)
        {
            var dev = offline.DeviceId!;
            var seq = offline.ClientSequenceNumber.Value;

            var lastSeq = await _context.OfflineTransactions
                .AsNoTracking()
                .Where(x => x.CashRegisterId == offline.CashRegisterId &&
                            x.DeviceId == dev &&
                            x.ClientSequenceNumber != null)
                .MaxAsync(x => x.ClientSequenceNumber)
                .ConfigureAwait(false);

            if (lastSeq != null)
            {
                if (seq <= lastSeq.Value)
                {
                    offline.SequenceDuplicateDetected = true;
                    offline.SequenceGapDetected = false;
                    offline.Status = OfflineTransactionStatus.Failed;
                    offline.LastErrorCode = "SEQUENCE_DUPLICATE";
                    offline.LastErrorMessageSafe = "Duplicate or non-monotonic client sequence number detected.";
                    offline.UpdatedAt = now;

                    await TryWriteSequenceDuplicateAuditAsync(
                        offline,
                        seq,
                        lastSeq.Value,
                        userId,
                        userRole,
                        replayBatchCorrelationId,
                        replayBatchAuditKey).ConfigureAwait(false);
                }
                else if (seq > lastSeq.Value + 1)
                {
                    offline.SequenceGapDetected = true;
                    await TryWriteSequenceGapAuditAsync(
                        offline,
                        seq,
                        lastSeq.Value,
                        userId,
                        userRole,
                        replayBatchCorrelationId,
                        replayBatchAuditKey).ConfigureAwait(false);
                }
            }
        }

        _context.OfflineTransactions.Add(offline);
        try
        {
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // In case of concurrent insert conflicts (payload hash unique constraint),
            // re-load by payload hash and return the canonical row.
            _context.Entry(offline).State = EntityState.Detached;
            var existing = await _context.OfflineTransactions
                .FirstOrDefaultAsync(x =>
                    x.CashRegisterId == offline.CashRegisterId &&
                    x.PayloadHash == prepared.PayloadHashHex)
                .ConfigureAwait(false);
            if (existing != null)
                return existing;

            throw;
        }

        // OFFLINE_CREATED audit: record was received by backend.
        await TryWriteOfflineCreatedAuditAsync(
            offline,
            prepared.StoredPayloadJson,
            userId,
            userRole,
            replayBatchCorrelationId,
            replayBatchAuditKey).ConfigureAwait(false);

        return offline;
    }

    private static string GetPayloadRaw(JsonElement payload)
    {
        return payload.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : payload.GetRawText();
    }

    /// <summary>
    /// Match replay hash to rows whose stored JSON normalizes to the same SHA-256 as the client.
    /// </summary>
    private async Task<OfflineTransaction?> TryResolveOfflineByRuntimeRecomputedHashAsync(
        Guid cashRegisterId,
        string payloadHashHex)
    {
        var candidates = await _context.OfflineTransactions
            .AsNoTracking()
            .Where(x => x.CashRegisterId == cashRegisterId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(RuntimeRecomputedHashCandidateLimit)
            .Select(x => new { x.Id, x.PayloadJson, x.PayloadSecretsProtected })
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c.PayloadJson))
                continue;
            string h;
            try
            {
                var fullJson = OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                    c.PayloadJson,
                    c.PayloadSecretsProtected,
                    _offlineFullPayloadProtector,
                    GetOfflineVoucherFieldAesKeyBytes());
                h = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(fullJson);
            }
            catch
            {
                continue;
            }

            if (!string.Equals(h, payloadHashHex, StringComparison.OrdinalIgnoreCase))
                continue;

            return await _context.OfflineTransactions
                .FirstOrDefaultAsync(x => x.Id == c.Id)
                .ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// After successful payload match, persist runtime-canonical hash when DB still has legacy digest.
    /// Skips update if another row already owns the canonical (CashRegisterId, payload_hash).
    /// Returns true if alignment was performed (for audit: PayloadRepaired).
    /// </summary>
    private async Task<bool> TryAlignStoredPayloadHashToRuntimeCanonicalAsync(Guid offlineTransactionId)
    {
        var row = await _context.OfflineTransactions
            .FirstOrDefaultAsync(x => x.Id == offlineTransactionId)
            .ConfigureAwait(false);
        if (row == null || string.IsNullOrWhiteSpace(row.PayloadJson))
            return false;

        string canonical;
        try
        {
            var fullJson = OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                row.PayloadJson,
                row.PayloadSecretsProtected,
                _offlineFullPayloadProtector,
                GetOfflineVoucherFieldAesKeyBytes());
            canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(fullJson);
        }
        catch
        {
            return false;
        }

        if (string.Equals(row.PayloadHash, canonical, StringComparison.OrdinalIgnoreCase))
            return false;

        var conflict = await _context.OfflineTransactions.AnyAsync(x =>
                x.CashRegisterId == row.CashRegisterId &&
                x.PayloadHash == canonical &&
                x.Id != row.Id)
            .ConfigureAwait(false);
        if (conflict)
        {
            _logger.LogDebug(
                "Skip payload_hash align for offline {Id}: canonical hash already used on this register",
                offlineTransactionId);
            return false;
        }

        row.PayloadHash = canonical;
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync().ConfigureAwait(false);
        _metrics?.RecordPayloadHashMismatch(1);
        _logger.LogInformation(
            "Aligned OfflineTransaction {Id} payload_hash to runtime canonical (lazy repair on replay)",
            row.Id);
        return true;
    }

    /// <summary>
    /// Last-resort: structural equality when hash recomputation did not match (e.g. numeric/format edge cases).
    /// Only returns a match when it is unique in the window to avoid wrong intent resolution.
    /// </summary>
    private async Task<OfflineTransaction?> TryResolveOfflineByStructuralPayloadAsync(
        Guid cashRegisterId,
        string normalizedPayloadJson)
    {
        var limit = Math.Max(1, Math.Min(500, _replayOptions.StructuralPayloadFallbackLimit));
        var candidates = await _context.OfflineTransactions
            .AsNoTracking()
            .Where(x => x.CashRegisterId == cashRegisterId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        var matches = candidates
            .Where(x => OfflinePayloadComparer.EqualsNormalized(
                OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                    x.PayloadJson,
                    x.PayloadSecretsProtected,
                    _offlineFullPayloadProtector,
                    GetOfflineVoucherFieldAesKeyBytes()),
                normalizedPayloadJson))
            .ToList();
        if (matches.Count != 1)
        {
            if (matches.Count > 1)
            {
                _metrics?.RecordStructuralFallbackAmbiguous(1);
                _logger.LogDebug(
                    "Offline structural fallback: ambiguous match for CashRegisterId={CashRegisterId}, {Count} rows match; skipping.",
                    cashRegisterId, matches.Count);
            }
            return null;
        }

        _metrics?.RecordStructuralFallbackResolved(1);
        var match = matches[0];
        _logger.LogInformation(
            "Offline resolved by structural fallback: CashRegisterId={CashRegisterId}, OfflineId={OfflineId} (hash path did not match).",
            cashRegisterId, match.Id);
        return await _context.OfflineTransactions
            .FirstOrDefaultAsync(x => x.Id == match.Id)
            .ConfigureAwait(false);
    }

    private static int ComputeBackoffSeconds(int retryCount)
    {
        // retryCount is already incremented for the current attempt.
        // First retry failure -> exponent 0.
        var exp = Math.Max(0, retryCount - 1);
        var seconds = (int)(BackoffBaseSeconds * Math.Pow(2, exp));
        return Math.Min(BackoffMaxSeconds, Math.Max(0, seconds));
    }

    private (string Code, string SafeMessage) MapPaymentFailure(PaymentResult paymentResult)
    {
        var code = string.IsNullOrWhiteSpace(paymentResult.DiagnosticCode)
            ? (paymentResult.IsDeterministicFailure ? "VALIDATION_FAILED" : "PAYMENT_FAILED")
            : paymentResult.DiagnosticCode.Trim();

        var msg = string.IsNullOrWhiteSpace(paymentResult.Message)
            ? "Payment creation failed."
            : SanitizeUserMessage(paymentResult.Message);

        return (code, msg);
    }

    private static string SanitizeUserMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        var s = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length <= 500 ? s : s[..500];
    }

    private static string SanitizeExceptionMessage(Exception ex)
    {
        var s = (ex.Message ?? "Replay error").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length <= 500 ? s : s[..500];
    }

    private static ReplayOfflineTransactionsResponseItem SuccessSyncedItem(
        Guid requestedId,
        Guid resolvedId,
        Guid syncedPaymentId,
        int retryCount,
        Guid replayBatchCorrelationId) =>
        new()
        {
            RequestedOfflineTransactionId = requestedId,
            OfflineTransactionId = resolvedId,
            Status = OfflineTransactionStatus.Synced.ToString(),
            SyncedPaymentId = syncedPaymentId,
            Error = null,
            ErrorCode = null,
            RetryCount = retryCount,
            LastErrorMessageSafe = null,
            ExponentialBackoffHintSeconds = null,
            ReplayBatchCorrelationId = replayBatchCorrelationId
        };

    private static ReplayOfflineTransactionsResponseItem FailedLocalItem(
        Guid requestedId,
        Guid resolvedId,
        string errorCode,
        string message,
        int retryCount,
        Guid replayBatchCorrelationId) =>
        new()
        {
            RequestedOfflineTransactionId = requestedId,
            OfflineTransactionId = resolvedId,
            Status = OfflineTransactionStatus.Failed.ToString(),
            SyncedPaymentId = null,
            Error = message,
            ErrorCode = errorCode,
            RetryCount = retryCount,
            LastErrorMessageSafe = message,
            ExponentialBackoffHintSeconds = null,
            ReplayBatchCorrelationId = replayBatchCorrelationId
        };

    private static ReplayOfflineTransactionsResponseItem FailedFromRow(
        Guid requestedId,
        Guid resolvedId,
        OfflineTransaction row,
        Guid replayBatchCorrelationId) =>
        new()
        {
            RequestedOfflineTransactionId = requestedId,
            OfflineTransactionId = resolvedId,
            Status = row.Status.ToString(),
            SyncedPaymentId = row.SyncedPaymentId,
            Error = row.LastErrorMessageSafe ?? row.LastErrorCode,
            ErrorCode = row.LastErrorCode,
            RetryCount = row.RetryCount,
            LastErrorMessageSafe = row.LastErrorMessageSafe,
            ExponentialBackoffHintSeconds = null,
            ReplayBatchCorrelationId = replayBatchCorrelationId
        };

    private static ReplayOfflineTransactionsResponseItem PendingOrFailedFromAttempt(
        Guid requestedId,
        Guid resolvedId,
        OfflineTransaction row,
        string? errCode,
        string? safeMsg,
        int? nextBackoffHintSeconds,
        Guid replayBatchCorrelationId) =>
        new()
        {
            RequestedOfflineTransactionId = requestedId,
            OfflineTransactionId = resolvedId,
            Status = row.Status.ToString(),
            SyncedPaymentId = row.SyncedPaymentId,
            Error = safeMsg,
            ErrorCode = errCode,
            RetryCount = row.RetryCount,
            LastErrorMessageSafe = row.LastErrorMessageSafe ?? safeMsg,
            ExponentialBackoffHintSeconds = row.Status is OfflineTransactionStatus.Pending or OfflineTransactionStatus.NonFiscalPending
                ? nextBackoffHintSeconds
                : null,
            ReplayBatchCorrelationId = replayBatchCorrelationId
        };

    /// <summary>
    /// Observability: record DeviceId/ClientSequence coverage for this replayed intent.
    /// Best-effort; replay never fails if this throws.
    /// </summary>
    private async Task RecordOfflineIntentCoverageAsync(
        Guid cashRegisterId,
        string? deviceId,
        int? clientSequenceNumber,
        Guid replayBatchCorrelationId)
    {
        try
        {
            var sample = new OfflineIntentCoverageSample
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                HasDeviceId = !string.IsNullOrWhiteSpace(deviceId),
                HasClientSequence = clientSequenceNumber.HasValue,
                ReplayBatchCorrelationId = replayBatchCorrelationId
            };
            _context.OfflineIntentCoverageSamples.Add(sample);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Offline intent coverage sample insert failed for CashRegisterId={CashRegisterId}; replay continues.",
                cashRegisterId);
        }
    }

    private async Task TryWriteOfflineCreatedAuditAsync(
        OfflineTransaction offline,
        string payloadRawForAuditSafe,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey)
    {
        try
        {
            var safe = BuildSafeCreatePaymentSnapshot(payloadRawForAuditSafe);

            await _auditLogService.LogSystemOperationAsync(
                    "OFFLINE_CREATED",
                    "OfflineTransaction",
                    userId,
                    userRole,
                    description: $"OfflineTransaction received (Status={offline.Status}) CashRegisterId={offline.CashRegisterId}",
                    requestData: safe,
                    responseData: new
                    {
                        offline.Id,
                        offline.CashRegisterId,
                        offline.OfflineCreatedAtUtc,
                        offline.ServerReceivedAtUtc,
                        offline.Status,
                        offline.DeviceId,
                        offline.ClientSequenceNumber,
                        offline.ClockDriftWarning,
                        offline.SequenceGapDetected,
                        offline.SequenceDuplicateDetected,
                        replayBatchCorrelationId
                    },
                    correlationIdOverride: replayBatchAuditKey)
                .ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "OFFLINE_CREATED audit log failed for OfflineTransactionId={OfflineId}", offline.Id);
        }
    }

    private async Task TryWriteClockDriftAuditAsync(
        OfflineTransaction offline,
        TimeSpan drift,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                "CLOCK_DRIFT_WARNING",
                "OfflineTransaction",
                userId,
                userRole,
                description:
                    $"Clock drift warning: OfflineCreatedAtUtc is {drift.TotalMinutes:F2} minutes ahead of server. OfflineId={offline.Id}",
                requestData: new { offline.Id, replayBatchCorrelationId },
                responseData: new
                {
                    offline.OfflineCreatedAtUtc,
                    offline.ServerReceivedAtUtc,
                    driftSeconds = (long)drift.TotalSeconds,
                    replayBatchCorrelationId
                },
                correlationIdOverride: replayBatchAuditKey).ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "CLOCK_DRIFT_WARNING audit log failed for OfflineTransactionId={OfflineId}", offline.Id);
        }
    }

    private async Task TryWriteSequenceGapAuditAsync(
        OfflineTransaction offline,
        int seq,
        int lastSeq,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                "SEQUENCE_GAP_DETECTED",
                "OfflineTransaction",
                userId,
                userRole,
                description:
                    $"Client sequence gap detected for DeviceId={offline.DeviceId}, CashRegisterId={offline.CashRegisterId}. Last={lastSeq}, Incoming={seq}.",
                requestData: new { deviceId = offline.DeviceId, cashRegisterId = offline.CashRegisterId, replayBatchCorrelationId },
                responseData: new { offline.Id, lastSeq, seq, offline.DeviceId, offline.ClientSequenceNumber, replayBatchCorrelationId },
                correlationIdOverride: replayBatchAuditKey
            ).ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "SEQUENCE_GAP_DETECTED audit log failed for OfflineTransactionId={OfflineId}", offline.Id);
        }
    }

    private async Task TryWriteSequenceDuplicateAuditAsync(
        OfflineTransaction offline,
        int seq,
        int lastSeq,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                "SEQUENCE_DUPLICATE",
                "OfflineTransaction",
                userId,
                userRole,
                description:
                    $"Client sequence duplicate/non-monotonic detected for DeviceId={offline.DeviceId}, CashRegisterId={offline.CashRegisterId}. Last={lastSeq}, Incoming={seq}.",
                requestData: new { deviceId = offline.DeviceId, cashRegisterId = offline.CashRegisterId, replayBatchCorrelationId },
                responseData: new { offline.Id, lastSeq, seq, offline.DeviceId, offline.ClientSequenceNumber, replayBatchCorrelationId },
                correlationIdOverride: replayBatchAuditKey
            ).ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "SEQUENCE_DUPLICATE audit log failed for OfflineTransactionId={OfflineId}", offline.Id);
        }
    }

    private async Task TryWriteOfflinePayloadDedupAuditAsync(
        OfflineTransaction canonical,
        Guid requestedOfflineId,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey,
        string replayPath)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                "PAYLOAD_HASH_DEDUPLICATED",
                "OfflineTransaction",
                userId,
                userRole,
                description:
                    $"Payload hash conflict resolution: requested OfflineTransactionId={requestedOfflineId} resolved to canonical OfflineTransactionId={canonical.Id}. ReplayPath={replayPath}.",
                requestData: new { requestedOfflineId, replayBatchCorrelationId, replayPath },
                responseData: new { canonical.Id, canonical.CashRegisterId, canonical.PayloadHash, replayBatchCorrelationId, replayPath },
                correlationIdOverride: replayBatchAuditKey)
                .ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "PAYLOAD_HASH_DEDUPLICATED audit log failed for requestedOfflineId={RequestedOfflineId}", requestedOfflineId);
        }
    }

    private async Task TryWriteOfflineSyncedAuditAsync(
        OfflineTransaction offline,
        Guid syncedPaymentId,
        string userId,
        string userRole,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey,
        string replayPath,
        bool payloadRepaired)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                "OFFLINE_SYNCED",
                "OfflineTransaction",
                userId,
                userRole,
                description: $"OfflineTransaction replayed and synced to PaymentId={syncedPaymentId}. ReplayPath={replayPath}, PayloadRepaired={payloadRepaired}.",
                requestData: new { offline.Id, replayBatchCorrelationId, replayPath, payloadRepaired },
                responseData: new
                {
                    offline.Id,
                    offline.CashRegisterId,
                    offline.OfflineCreatedAtUtc,
                    offline.ServerReceivedAtUtc,
                    offline.Status,
                    syncedPaymentId,
                    replayBatchCorrelationId,
                    replayPath,
                    payloadRepaired
                },
                correlationIdOverride: replayBatchAuditKey).ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "OFFLINE_SYNCED audit log failed for OfflineTransactionId={OfflineId}", offline.Id);
        }
    }

    private async Task TryWriteOfflineFailedAuditAsync(
        OfflineTransaction offline,
        string userId,
        string userRole,
        string actionCode,
        Guid replayBatchCorrelationId,
        string replayBatchAuditKey,
        string replayPath,
        bool payloadRepaired)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                actionCode,
                "OfflineTransaction",
                userId,
                userRole,
                description:
                    $"Offline replay failed. OfflineId={offline.Id}, ErrorCode={offline.LastErrorCode}. ReplayPath={replayPath}, PayloadRepaired={payloadRepaired}.",
                requestData: new { offline.Id, replayBatchCorrelationId, replayPath, payloadRepaired },
                responseData: new
                {
                    offline.Id,
                    offline.CashRegisterId,
                    offline.Status,
                    offline.LastReplayAttemptAt,
                    offline.RetryCount,
                    offline.LastErrorCode,
                    offline.LastErrorMessageSafe,
                    replayBatchCorrelationId,
                    replayPath,
                    payloadRepaired
                },
                correlationIdOverride: replayBatchAuditKey).ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "{AuditAction} audit log failed for OfflineTransactionId={OfflineId}", actionCode, offline.Id);
        }
    }

    private static object BuildSafeCreatePaymentSnapshot(string payloadRaw)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadRaw);
            var root = doc.RootElement;

            var customerId = root.TryGetProperty("customerId", out var c) ? c.GetRawText() : "null";
            var cashRegisterId = root.TryGetProperty("cashRegisterId", out var cr) ? cr.GetRawText() : "null";
            var idempotencyKey = root.TryGetProperty("idempotencyKey", out var ik) ? ik.GetString() : null;
            var tableNumber = root.TryGetProperty("tableNumber", out var tn) ? tn.GetInt32() : (int?)null;
            var totalAmount = root.TryGetProperty("totalAmount", out var ta) ? ta.GetDecimal() : (decimal?)null;
            var paymentMethod = root.TryGetProperty("payment", out var p) && p.TryGetProperty("method", out var m)
                ? m.GetString()
                : null;
            var itemsCount = root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                ? items.GetArrayLength()
                : 0;

            return new
            {
                customerId,
                cashRegisterId,
                idempotencyKey,
                tableNumber,
                totalAmount,
                paymentMethod,
                itemsCount
            };
        }
        catch
        {
            return new { parsing = "failed" };
        }
    }
}

