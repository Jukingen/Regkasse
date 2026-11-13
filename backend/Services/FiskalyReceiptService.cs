using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IFiskalyReceiptService
{
    Task<FiskalyReceiptOperationResult> CreateNormalReceiptAsync(
        Guid cashRegisterId,
        decimal? amount,
        string? vatRate,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CancelReceiptAsync(
        Guid cashRegisterId,
        Guid originalReceiptId,
        string reason,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CreateNullbelegAsync(
        Guid cashRegisterId,
        int? year,
        int? month,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CreateStartbelegAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CreateMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        string? reason,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CreateJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<FiskalyReceiptOperationResult> CreateSchlussbelegAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an existing Daily closing to Fiskaly SIGN AT as a 0.00 NORMAL marker receipt.
    /// Does not create a new RKSV closing and does not overwrite <see cref="DailyClosing.TseSignature"/>.
    /// </summary>
    Task<FiskalyReceiptOperationResult> CreateTagesabschlussAsync(
        Guid closingId,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the TSE-signed Daily closing for the register and Vienna day, then submits it to Fiskaly.
    /// Does not create a new closing.
    /// </summary>
    Task<FiskalyReceiptOperationResult> CreateTagesabschlussAsync(
        Guid cashRegisterId,
        DateTime? closingDate,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}

public sealed class FiskalyReceiptOperationResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; } = 200;

    public FiskalyReceiptDataDto? Data { get; init; }

    public FiskalyReceiptErrorDto? Error { get; init; }

    public Guid? HistoryId { get; set; }

    public static FiskalyReceiptOperationResult Ok(FiskalyReceiptDataDto data) =>
        new() { Success = true, StatusCode = 200, Data = data };

    public static FiskalyReceiptOperationResult Fail(
        int statusCode,
        string code,
        string message,
        string? details = null) =>
        new()
        {
            Success = false,
            StatusCode = statusCode,
            Error = FiskalyReceiptErrorMapper.FromCode(code, message, details)
        };
}

/// <summary>
/// Tenant-scoped facade for Fiskaly/RKSV receipt operations.
/// Synthetic NORMAL signs via the Development TEST helper (LIVE blocked).
/// Storno uses <see cref="IPaymentService"/> (real CANCELLATION).
/// Sonderbelege delegate to <see cref="IRksvSpecialReceiptService"/> (no parallel Fiskaly-only path).
/// Cross-tenant cash registers resolve as not found (HTTP 404), not 403.
/// </summary>
public sealed class FiskalyReceiptService : IFiskalyReceiptService
{
    private readonly IFiskalySignTestService _signTest;
    private readonly IPaymentService _payments;
    private readonly IRksvSpecialReceiptService _specialReceipts;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentTenantAccessor? _tenantAccessor;
    private readonly IFiskalyOperationHistoryRecorder? _history;
    private readonly FiskalyOperationHistoryWriteScope? _historyScope;
    private readonly AppDbContext? _db;
    private readonly IFiskalyTseService? _fiskalyTse;
    private readonly IOptionsMonitor<FiskalyOptions>? _fiskalyOptions;
    private readonly FiskalyEnabledOverrideCache? _fiskalyEnabledCache;
    private readonly ILogger<FiskalyReceiptService> _logger;

    public FiskalyReceiptService(
        IFiskalySignTestService signTest,
        IPaymentService payments,
        IRksvSpecialReceiptService specialReceipts,
        IAuditLogService auditLog,
        ILogger<FiskalyReceiptService> logger,
        ICurrentTenantAccessor? tenantAccessor = null,
        IFiskalyOperationHistoryRecorder? history = null,
        FiskalyOperationHistoryWriteScope? historyScope = null,
        AppDbContext? db = null,
        IFiskalyTseService? fiskalyTse = null,
        IOptionsMonitor<FiskalyOptions>? fiskalyOptions = null,
        FiskalyEnabledOverrideCache? fiskalyEnabledCache = null)
    {
        _signTest = signTest;
        _payments = payments;
        _specialReceipts = specialReceipts;
        _auditLog = auditLog;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
        _history = history;
        _historyScope = historyScope;
        _db = db;
        _fiskalyTse = fiskalyTse;
        _fiskalyOptions = fiskalyOptions;
        _fiskalyEnabledCache = fiskalyEnabledCache;
    }

    public async Task<FiskalyReceiptOperationResult> CreateNormalReceiptAsync(
        Guid cashRegisterId,
        decimal? amount,
        string? vatRate,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        await BeginHistoryAsync(
                FiskalyOperationTypes.Normal,
                cashRegisterId,
                actorUserId,
                new { cashRegisterId, amount, vatRate },
                cancellationToken)
            .ConfigureAwait(false);

        var signed = await _signTest
            .SignAsync(
                new FiskalySignTestRequest
                {
                    CashRegisterId = cashRegisterId,
                    Scenario = FiskalySignTestScenarioIds.Normal,
                    Amount = amount,
                    VatRate = vatRate
                },
                actorUserId,
                actorIsSuperAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        var mapped = FromSignTest(signed);
        await AuditAsync(
                actorUserId,
                "normal",
                cashRegisterId,
                mapped,
                cancellationToken,
                actorIsSuperAdmin)
            .ConfigureAwait(false);
        return mapped;
    }

    public async Task<FiskalyReceiptOperationResult> CancelReceiptAsync(
        Guid cashRegisterId,
        Guid originalReceiptId,
        string reason,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        await BeginHistoryAsync(
                FiskalyOperationTypes.Cancel,
                cashRegisterId,
                actorUserId,
                new { cashRegisterId, originalReceiptId, reason },
                cancellationToken)
            .ConfigureAwait(false);

        if (cashRegisterId == Guid.Empty)
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.CashRegisterIdRequired,
                    "Cash register id is required.",
                    cancellationToken)
                .ConfigureAwait(false);

        if (originalReceiptId == Guid.Empty)
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.ValidationError,
                    "Original receipt (payment) id is required.",
                    cancellationToken)
                .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.ValidationError,
                    "Cancellation reason must be at least 5 characters.",
                    cancellationToken)
                .ConfigureAwait(false);

        PaymentDetails? original;
        try
        {
            original = await _payments.GetPaymentAsync(originalReceiptId).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    404,
                    FiskalyReceiptErrorCodes.PaymentNotFound,
                    "Payment not found.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly cancel: failed to load payment {PaymentId}", originalReceiptId);
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.StornoFailed,
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (original is null)
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    404,
                    FiskalyReceiptErrorCodes.PaymentNotFound,
                    "Payment not found.",
                    cancellationToken)
                .ConfigureAwait(false);

        if (original.CashRegisterId != cashRegisterId)
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    404,
                    FiskalyReceiptErrorCodes.CashRegisterMismatch,
                    "Payment not found.",
                    cancellationToken)
                .ConfigureAwait(false);

        PaymentResult result;
        try
        {
            result = await _payments
                .CancelPaymentAsync(
                    originalReceiptId,
                    reason.Trim(),
                    actorUserId,
                    idempotencyKey: null,
                    CancellationReasonCode.Other)
                .ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromException(ex);
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details)
                .ConfigureAwait(false);
        }
        catch (TseUnavailableException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromTseUnavailable(ex);
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.StornoFailed,
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.RequiresApproval)
        {
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.ApprovalRequired,
                    result.Message,
                    cancellationToken,
                    result.DiagnosticCode)
                .ConfigureAwait(false);
        }

        if (!result.Success)
        {
            var details = result.Errors.Count > 0 ? string.Join("; ", result.Errors) : result.DiagnosticCode;
            return await FailAndAuditAsync(
                    actorUserId,
                    "cancel",
                    cashRegisterId,
                    400,
                    result.DiagnosticCode ?? FiskalyReceiptErrorCodes.StornoFailed,
                    string.IsNullOrWhiteSpace(result.Message) ? "Storno failed." : result.Message,
                    cancellationToken,
                    details)
                .ConfigureAwait(false);
        }

        var ok = FiskalyReceiptOperationResult.Ok(FromPaymentResult(result));
        await AuditAsync(actorUserId, "cancel", cashRegisterId, ok, cancellationToken, actorIsSuperAdmin).ConfigureAwait(false);
        return ok;
    }

    public Task<FiskalyReceiptOperationResult> CreateNullbelegAsync(
        Guid cashRegisterId,
        int? year,
        int? month,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        RunSpecialAsync(
            FiskalyOperationTypes.Nullbeleg,
            cashRegisterId,
            actorUserId,
            () => _specialReceipts.CreateNullbelegAsync(
                new CreateNullbelegRequest
                {
                    CashRegisterId = cashRegisterId,
                    Year = year,
                    Month = month,
                    Reason = reason
                },
                actorUserId,
                cancellationToken),
            r => FromSpecial(r.ReceiptId, r.ReceiptNumber, qrCode: null),
            cancellationToken,
            requestPayload: new { cashRegisterId, year, month, reason });

    public Task<FiskalyReceiptOperationResult> CreateStartbelegAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        RunSpecialAsync(
            FiskalyOperationTypes.Startbeleg,
            cashRegisterId,
            actorUserId,
            () => _specialReceipts.CreateStartbelegAsync(
                new CreateStartbelegRequest
                {
                    CashRegisterId = cashRegisterId,
                    Reason = reason ?? string.Empty
                },
                actorUserId,
                cancellationToken),
            r => FromSpecial(r.ReceiptId, r.ReceiptNumber, r.QrData),
            cancellationToken,
            requestPayload: new { cashRegisterId, reason });

    public Task<FiskalyReceiptOperationResult> CreateMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        string? reason,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default) =>
        RunSpecialAsync(
            FiskalyOperationTypes.Monatsbeleg,
            cashRegisterId,
            actorUserId,
            async () =>
            {
                await EnsureStartbelegPresentAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
                return await _specialReceipts.CreateMonatsbelegAsync(
                    new CreateMonatsbelegRequest
                    {
                        CashRegisterId = cashRegisterId,
                        Year = year,
                        Month = month,
                        Reason = reason
                    },
                    actorUserId,
                    forcePastMonth: actorIsSuperAdmin,
                    cancellationToken).ConfigureAwait(false);
            },
            r => FromSpecial(r.ReceiptId, r.ReceiptNumber, r.QrData),
            cancellationToken,
            actorIsSuperAdmin,
            new { cashRegisterId, year, month, reason });

    public Task<FiskalyReceiptOperationResult> CreateJahresbelegAsync(
        Guid cashRegisterId,
        int year,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        RunSpecialAsync(
            FiskalyOperationTypes.Jahresbeleg,
            cashRegisterId,
            actorUserId,
            async () =>
            {
                await EnsureStartbelegPresentAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
                return await _specialReceipts.CreateJahresbelegAsync(
                    new CreateJahresbelegRequest
                    {
                        CashRegisterId = cashRegisterId,
                        Year = year,
                        Reason = reason
                    },
                    actorUserId,
                    cancellationToken).ConfigureAwait(false);
            },
            r => FromSpecial(r.ReceiptId, r.ReceiptNumber, r.QrData),
            cancellationToken,
            requestPayload: new { cashRegisterId, year, reason });

    public Task<FiskalyReceiptOperationResult> CreateSchlussbelegAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        RunSpecialAsync(
            FiskalyOperationTypes.Schlussbeleg,
            cashRegisterId,
            actorUserId,
            () => _specialReceipts.CreateSchlussbelegAsync(
                new CreateSchlussbelegRequest
                {
                    CashRegisterId = cashRegisterId,
                    Reason = reason
                },
                actorUserId,
                cancellationToken),
            r => FromSpecial(r.ReceiptId, r.ReceiptNumber, r.QrData),
            cancellationToken,
            requestPayload: new { cashRegisterId, reason });

    public async Task<FiskalyReceiptOperationResult> CreateTagesabschlussAsync(
        Guid cashRegisterId,
        DateTime? closingDate,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (_db is null)
            return FiskalyReceiptOperationResult.Fail(
                400,
                FiskalyReceiptErrorCodes.FiskalyNotConfigured,
                "Fiskaly receipt service is not configured for daily closing submission.");

        if (cashRegisterId == Guid.Empty)
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    Guid.Empty,
                    400,
                    FiskalyReceiptErrorCodes.CashRegisterIdRequired,
                    "Cash register id is required.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        var resolve = TryResolveDailyClosingBusinessDay(closingDate);
        if (resolve.ErrorMessage is not null)
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    cashRegisterId,
                    400,
                    resolve.ErrorCode ?? FiskalyReceiptErrorCodes.ValidationError,
                    resolve.ErrorMessage,
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(resolve.BusinessDay);
        var closing = await _db.DailyClosings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CashRegisterId == cashRegisterId
                     && c.ClosingType == "Daily"
                     && c.ClosingDate == closingAnchorUtc,
                cancellationToken)
            .ConfigureAwait(false);

        if (closing is null)
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    cashRegisterId,
                    404,
                    FiskalyReceiptErrorCodes.ClosingNotFound,
                    "Daily closing not found for this cash register and date. Create a TSE-signed Tagesabschluss first.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        return await CreateTagesabschlussAsync(closing.Id, actorUserId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FiskalyReceiptOperationResult> CreateTagesabschlussAsync(
        Guid closingId,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (_db is null || _fiskalyTse is null)
            return FiskalyReceiptOperationResult.Fail(
                400,
                FiskalyReceiptErrorCodes.FiskalyNotConfigured,
                "Fiskaly receipt service is not configured for daily closing submission.");

        if (closingId == Guid.Empty)
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    Guid.Empty,
                    400,
                    FiskalyReceiptErrorCodes.ValidationError,
                    "Closing id is required.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        var closing = await _db.DailyClosings
            .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken)
            .ConfigureAwait(false);
        if (closing is null)
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    Guid.Empty,
                    404,
                    FiskalyReceiptErrorCodes.ClosingNotFound,
                    "Daily closing not found.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        await BeginHistoryAsync(
                FiskalyOperationTypes.Tagesabschluss,
                closing.CashRegisterId,
                actorUserId,
                new
                {
                    closingId = closing.Id,
                    cashRegisterId = closing.CashRegisterId,
                    closingDate = closing.ClosingDate,
                    totalAmount = closing.TotalAmount,
                    transactionCount = closing.TransactionCount
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(closing.ClosingType, "Daily", StringComparison.OrdinalIgnoreCase))
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.ClosingNotDaily,
                    "Only daily closings can be submitted to Fiskaly as Tagesabschluss.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(closing.TseSignature))
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.TseSignatureRequired,
                    "Daily closing must be TSE signed before it can be sent to Fiskaly.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        if (DailyClosingFiskalyStatuses.IsSubmitted(closing.FiskalyStatus)
            && !string.IsNullOrWhiteSpace(closing.FiskalyReceiptId))
        {
            var already = FiskalyReceiptOperationResult.Ok(FromClosing(closing));
            await AuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    already,
                    cancellationToken,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
            return already;
        }

        if (_fiskalyOptions?.CurrentValue.HasActiveCredentials(_fiskalyEnabledCache?.OverrideEnabled) != true)
        {
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Skipped, closing.FiskalyReceiptId, "Fiskaly is disabled.");
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.FiskalyDisabled,
                    "Fiskaly is disabled.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }

        var ready = await _fiskalyTse
            .IsReadyToSignAsync(closing.CashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!ready)
        {
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Failed, closing.FiskalyReceiptId, "Fiskaly SCU or cash register is not INITIALIZED.");
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.FiskalyRegisterNotInitialized,
                    "Fiskaly SCU or cash register is not INITIALIZED.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }

        if (!Guid.TryParse(closing.FiskalyReceiptId, out var fiskalyReceiptId) || fiskalyReceiptId == Guid.Empty)
            fiskalyReceiptId = Guid.NewGuid();

        StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Pending, fiskalyReceiptId.ToString("D"), error: null);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var data = FiskalyReceiptSchemaMapper.BuildTagesabschlussTransaction(
            closing.CashRegisterId,
            closing.ClosingDate,
            closing.TotalAmount,
            closing.TransactionCount);

        try
        {
            var signed = await _fiskalyTse
                .SignTransactionAsync(
                    tssId: string.Empty,
                    txId: fiskalyReceiptId.ToString("D"),
                    data,
                    cancellationToken)
                .ConfigureAwait(false);

            var remoteId = string.IsNullOrWhiteSpace(signed.Id)
                ? fiskalyReceiptId.ToString("D")
                : signed.Id.Trim();
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Submitted, remoteId, error: null);
            closing.FiskalySubmittedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var ok = FiskalyReceiptOperationResult.Ok(FromClosing(closing, signed.QrCodeData));
            await AuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    ok,
                    cancellationToken,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
            return ok;
        }
        catch (FiskalyApiException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromException(ex);
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Failed, fiskalyReceiptId.ToString("D"), err.Message);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (TseUnavailableException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromTseUnavailable(ex);
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Failed, fiskalyReceiptId.ToString("D"), err.Message);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly Tagesabschluss submit failed for closing {ClosingId}", closing.Id);
            StampClosingFiskaly(closing, DailyClosingFiskalyStatuses.Failed, fiskalyReceiptId.ToString("D"), ex.Message);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return await FailAndAuditAsync(
                    actorUserId,
                    FiskalyOperationTypes.Tagesabschluss,
                    closing.CashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.FiskalyApiError,
                    ex.Message,
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
    }

    private static (DateTime BusinessDay, string? ErrorCode, string? ErrorMessage) TryResolveDailyClosingBusinessDay(
        DateTime? closingDate)
    {
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        if (!closingDate.HasValue)
            return (viennaToday, null, null);

        // Date-picker Unspecified and persisted UTC anchors both map to the Vienna calendar day.
        var businessDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closingDate.Value);
        if (businessDay > viennaToday)
            return (
                businessDay,
                FiskalyReceiptErrorCodes.FutureClosingDate,
                "Daily closing cannot be submitted for a future date.");

        return (businessDay, null, null);
    }

    private static void StampClosingFiskaly(
        DailyClosing closing,
        string status,
        string? receiptId,
        string? error)
    {
        closing.FiskalyStatus = status;
        if (!string.IsNullOrWhiteSpace(receiptId))
            closing.FiskalyReceiptId = receiptId;
        closing.FiskalyError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        if (closing.FiskalyError is { Length: > 500 } longError)
            closing.FiskalyError = longError[..500];
        closing.UpdatedAt = DateTime.UtcNow;
    }

    private static FiskalyReceiptDataDto FromClosing(DailyClosing closing, string? fiskalyQr = null) =>
        new()
        {
            ReceiptId = closing.FiskalyReceiptId ?? closing.Id.ToString("D"),
            ReceiptNumber = $"TA-{closing.ClosingDate:yyyyMMdd}",
            Signature = closing.TseSignature,
            QrCode = fiskalyQr
        };

    private async Task<FiskalyReceiptOperationResult> RunSpecialAsync<T>(
        string operation,
        Guid cashRegisterId,
        string actorUserId,
        Func<Task<T>> create,
        Func<T, FiskalyReceiptDataDto> map,
        CancellationToken cancellationToken,
        bool actorIsSuperAdmin = false,
        object? requestPayload = null)
    {
        await BeginHistoryAsync(
                operation,
                cashRegisterId,
                actorUserId,
                requestPayload ?? new { cashRegisterId },
                cancellationToken)
            .ConfigureAwait(false);

        if (cashRegisterId == Guid.Empty)
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    400,
                    FiskalyReceiptErrorCodes.CashRegisterIdRequired,
                    "Cash register id is required.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);

        try
        {
            var created = await create().ConfigureAwait(false);
            var ok = FiskalyReceiptOperationResult.Ok(map(created));
            await AuditAsync(actorUserId, operation, cashRegisterId, ok, cancellationToken, actorIsSuperAdmin).ConfigureAwait(false);
            return ok;
        }
        catch (UnauthorizedAccessException)
        {
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    404,
                    FiskalyReceiptErrorCodes.CashRegisterNotFound,
                    "Cash register not found.",
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (RksvOperationGuardException ex)
        {
            var conflict = IsDuplicateGuard(ex.ErrorCode);
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    conflict ? 409 : 400,
                    ex.ErrorCode,
                    ex.Message,
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromException(ex);
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (TseUnavailableException ex)
        {
            var err = FiskalyReceiptErrorMapper.FromTseUnavailable(ex);
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    400,
                    err.Code,
                    err.Message,
                    cancellationToken,
                    err.Details,
                    actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            var conflict = ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already permanently decommissioned", StringComparison.OrdinalIgnoreCase);
            var periodNotCompleted = ex.Message.Contains(
                "completed (past) Vienna calendar months",
                StringComparison.OrdinalIgnoreCase);
            var code = conflict
                ? "DUPLICATE_OR_DECOMMISSIONED"
                : periodNotCompleted
                    ? FiskalyReceiptErrorCodes.PeriodNotCompleted
                    : FiskalyReceiptErrorCodes.SpecialReceiptFailed;
            return await FailAndAuditAsync(
                    actorUserId,
                    operation,
                    cashRegisterId,
                    conflict ? 409 : 400,
                    code,
                    ex.Message,
                    cancellationToken,
                    actorIsSuperAdmin: actorIsSuperAdmin)
                .ConfigureAwait(false);
        }
    }

    private async Task EnsureStartbelegPresentAsync(Guid cashRegisterId, CancellationToken cancellationToken)
    {
        if (_db is null)
            return;

        var hasStartbeleg = await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == cashRegisterId
                    && p.IsActive
                    && p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Startbeleg,
                cancellationToken)
            .ConfigureAwait(false);
        if (hasStartbeleg)
            return;

        throw new RksvOperationGuardException(
            FiskalyReceiptErrorCodes.StartbelegRequired,
            "Startbeleg is required before creating Monatsbeleg or Jahresbeleg.");
    }

    private static bool IsDuplicateGuard(string errorCode) =>
        errorCode is RksvGuardErrorCodes.DuplicateStartbeleg
            or RksvGuardErrorCodes.DuplicateMonatsbeleg
            or RksvGuardErrorCodes.DuplicateJahresbeleg
            or RksvGuardErrorCodes.DuplicateSchlussbeleg
            or RksvGuardErrorCodes.RegisterAlreadyDecommissioned;

    private static FiskalyReceiptOperationResult FromSignTest(
        FiskalySetupOperationResult<FiskalySignTestResultDto> signed)
    {
        if (signed.Success && signed.Data is not null)
        {
            return FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = signed.Data.ReceiptId,
                ReceiptNumber = signed.Data.ReceiptNumber ?? string.Empty,
                Signature = signed.Data.QrCodeData,
                QrCode = signed.Data.QrCodeData
            });
        }

        return FiskalyReceiptOperationResult.Fail(
            signed.StatusCode,
            signed.Code ?? FiskalyReceiptErrorMapper.InferCodeFromMessage(signed.Message),
            signed.Message,
            signed.Details);
    }

    private static FiskalyReceiptDataDto FromPaymentResult(PaymentResult result)
    {
        var payment = result.Payment;
        var id = payment?.Id ?? result.PaymentId;
        return new FiskalyReceiptDataDto
        {
            ReceiptId = id?.ToString("D") ?? string.Empty,
            ReceiptNumber = payment?.ReceiptNumber ?? string.Empty,
            Signature = result.TseSignature ?? payment?.TseSignature,
            QrCode = result.QrPayload
        };
    }

    private static FiskalyReceiptDataDto FromSpecial(Guid receiptId, string receiptNumber, string? qrCode) =>
        new()
        {
            ReceiptId = receiptId.ToString("D"),
            ReceiptNumber = receiptNumber,
            Signature = qrCode,
            QrCode = qrCode
        };

    private async Task<FiskalyReceiptOperationResult> FailAndAuditAsync(
        string actorUserId,
        string operation,
        Guid cashRegisterId,
        int statusCode,
        string code,
        string message,
        CancellationToken cancellationToken,
        string? details = null,
        bool actorIsSuperAdmin = false)
    {
        var failed = FiskalyReceiptOperationResult.Fail(statusCode, code, message, details);
        await AuditAsync(actorUserId, operation, cashRegisterId, failed, cancellationToken, actorIsSuperAdmin).ConfigureAwait(false);
        return failed;
    }

    private async Task BeginHistoryAsync(
        string operation,
        Guid cashRegisterId,
        string actorUserId,
        object requestPayload,
        CancellationToken cancellationToken)
    {
        if (_history is null)
            return;

        try
        {
            var historyId = await _history
                .StartAsync(
                    new FiskalyOperationHistoryStartRequest
                    {
                        OperationType = operation,
                        CashRegisterId = cashRegisterId,
                        ActorUserId = actorUserId,
                        RequestPayload = requestPayload
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (historyId is Guid id && id != Guid.Empty)
            {
                await _history.MarkProcessingAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Fiskaly operation history for {Operation}", operation);
        }
    }

    private async Task AuditAsync(
        string actorUserId,
        string operation,
        Guid cashRegisterId,
        FiskalyReceiptOperationResult result,
        CancellationToken cancellationToken,
        bool actorIsSuperAdmin = false)
    {
        _ = cancellationToken;
        var success = result.Success;
        try
        {
            await _auditLog
                .LogSystemOperationAsync(
                    action: success
                        ? AuditLogActions.FISKALY_RECEIPT_SIGNED
                        : AuditLogActions.FISKALY_RECEIPT_OPERATION_FAILED,
                    entityType: "FiskalyReceipt",
                    userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    userRole: actorIsSuperAdmin ? Roles.SuperAdmin : Roles.Manager,
                    description: success
                        ? $"Fiskaly receipt operation succeeded ({operation})."
                        : $"Fiskaly receipt operation failed ({operation}).",
                    status: success ? AuditLogStatus.Success : AuditLogStatus.Failed,
                    errorDetails: success ? null : result.Error?.Code,
                    actionType: success
                        ? AuditEventType.FiskalyReceiptSigned
                        : AuditEventType.FiskalyReceiptOperationFailed,
                    tenantId: _tenantAccessor?.TenantId,
                    newValues: success
                        ? new
                        {
                            Operation = operation,
                            CashRegisterId = cashRegisterId,
                            result.Data?.ReceiptId,
                            result.Data?.ReceiptNumber
                        }
                        : new
                        {
                            Operation = operation,
                            CashRegisterId = cashRegisterId,
                            result.Error?.Code,
                            result.Error?.Message,
                            result.Error?.Details
                        })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Fiskaly receipt audit for {Operation}", operation);
        }

        await CompleteHistoryAsync(result, cancellationToken).ConfigureAwait(false);
        if (_historyScope?.CurrentHistoryId is Guid historyId && historyId != Guid.Empty)
            result.HistoryId = historyId;
    }

    private async Task CompleteHistoryAsync(
        FiskalyReceiptOperationResult result,
        CancellationToken cancellationToken)
    {
        var historyId = _historyScope?.CurrentHistoryId;
        if (_history is null || historyId is null || historyId == Guid.Empty)
            return;

        try
        {
            await _history
                .CompleteAsync(
                    historyId.Value,
                    new FiskalyOperationHistoryCompleteRequest
                    {
                        Success = result.Success,
                        ReceiptNumber = result.Data?.ReceiptNumber,
                        ReceiptId = result.Data?.ReceiptId,
                        ResponsePayload = result.Success
                            ? result.Data
                            : new { result.Error?.Code, result.Error?.Message, result.Error?.Details },
                        ErrorCode = result.Error?.Code,
                        ErrorMessage = result.Error?.Message
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to complete Fiskaly operation history {HistoryId}", historyId);
        }
    }
}
