using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Structured POS operator audit on the immutable <c>audit_logs</c> stream (via <see cref="IAuditLogService.LogPaymentOperationAsync"/>).
/// Payload uses <c>schema: pos_critical_audit_v1</c>; never stores voucher secrets or idempotency key values.
/// </summary>
public interface IPosCriticalActionAuditService
{
    Task LogPaymentOutcomeAsync(string userId, CreatePaymentRequest request, PaymentResult result,
        CancellationToken cancellationToken = default);

    Task LogPaymentUnhandledExceptionAsync(string userId, CreatePaymentRequest request, Exception exception,
        CancellationToken cancellationToken = default);

    Task LogEnsureReadyOutcomeAsync(string userId, PosCashRegisterContextDto dto,
        CancellationToken cancellationToken = default);

    Task LogSpecialReceiptOutcomeAsync(
        string userId,
        Guid cashRegisterId,
        string receiptKind,
        string result,
        AuditLogStatus status,
        string? errorCode = null,
        Guid? paymentId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>No-op implementation for tests or when audit wiring is omitted from a manual <see cref="PaymentService"/> ctor.</summary>
public static class PosCriticalActionAuditNoOp
{
    private sealed class Impl : IPosCriticalActionAuditService
    {
        public Task LogPaymentOutcomeAsync(string userId, CreatePaymentRequest request, PaymentResult result,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogPaymentUnhandledExceptionAsync(string userId, CreatePaymentRequest request, Exception exception,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogEnsureReadyOutcomeAsync(string userId, PosCashRegisterContextDto dto,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogSpecialReceiptOutcomeAsync(string userId, Guid cashRegisterId, string receiptKind, string result,
            AuditLogStatus status, string? errorCode = null, Guid? paymentId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public static readonly IPosCriticalActionAuditService Instance = new Impl();
}

public sealed class PosCriticalActionAuditService : IPosCriticalActionAuditService
{
    public const string Schema = "pos_critical_audit_v1";

    private readonly IAuditLogService _audit;
    private readonly IUserService _users;
    private readonly ILogger<PosCriticalActionAuditService> _logger;

    public PosCriticalActionAuditService(
        IAuditLogService audit,
        IUserService users,
        ILogger<PosCriticalActionAuditService> logger)
    {
        _audit = audit;
        _users = users;
        _logger = logger;
    }

    public async Task LogPaymentOutcomeAsync(string userId, CreatePaymentRequest request, PaymentResult result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await ResolveUserRoleAsync(userId).ConfigureAwait(false);
            var (resultLabel, status) = ClassifyPaymentResult(result);
            var method = (request.Payment?.Method ?? "").Trim().ToLowerInvariant();
            var usesVoucher = method == "voucher"
                || (request.Payment?.VoucherRedemptions?.Count ?? 0) > 0
                || !string.IsNullOrWhiteSpace(request.Payment?.VoucherCode);

            var payload = new
            {
                schema = Schema,
                actionKind = "payment_create",
                result = resultLabel,
                errorCode = result.DiagnosticCode,
                registerId = request.CashRegisterId == Guid.Empty ? (Guid?)null : request.CashRegisterId,
                hasIdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey),
                tableNumber = request.TableNumber,
                itemCount = request.Items?.Count ?? 0,
                clientTotalHint = request.TotalAmount,
                paymentMethod = string.IsNullOrEmpty(method) ? null : method,
                voucherRedemption = usesVoucher && result.Success,
                idempotentReplay = result.IdempotentReplay,
                invoicePersisted = result.InvoicePersisted,
                paymentId = result.PaymentId,
            };

            await _audit.LogPaymentOperationAsync(
                AuditLogActions.POS_PAY_OUTCOME,
                AuditLogEntityTypes.POS_CRITICAL,
                result.PaymentId,
                userId,
                role,
                amount: result.Payment?.TotalAmount ?? request.TotalAmount,
                paymentMethod: string.IsNullOrEmpty(method) ? null : method,
                correlationId: null,
                requestData: null,
                responseData: payload,
                description: "POS payment attempt outcome",
                status: status,
                errorDetails: result.Success ? null : Truncate(result.Message, 450)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PosCriticalActionAudit: payment outcome log failed for user {UserId}", userId);
        }
    }

    public async Task LogPaymentUnhandledExceptionAsync(string userId, CreatePaymentRequest request, Exception exception,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await ResolveUserRoleAsync(userId).ConfigureAwait(false);
            var payload = new
            {
                schema = Schema,
                actionKind = "payment_create",
                result = "failed",
                errorCode = "UNHANDLED_EXCEPTION",
                registerId = request.CashRegisterId == Guid.Empty ? (Guid?)null : request.CashRegisterId,
                hasIdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey),
                exceptionType = exception.GetType().Name,
            };

            await _audit.LogPaymentOperationAsync(
                AuditLogActions.POS_PAY_EX,
                AuditLogEntityTypes.POS_CRITICAL,
                entityId: null,
                userId,
                role,
                amount: request.TotalAmount,
                paymentMethod: (request.Payment?.Method ?? "").Trim().ToLowerInvariant(),
                correlationId: null,
                requestData: null,
                responseData: payload,
                description: "POS payment unhandled exception (before normalized PaymentResult)",
                status: AuditLogStatus.SystemError,
                errorDetails: Truncate(exception.Message, 450)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PosCriticalActionAudit: payment exception log failed for user {UserId}", userId);
        }
    }

    public async Task LogEnsureReadyOutcomeAsync(string userId, PosCashRegisterContextDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await ResolveUserRoleAsync(userId).ConfigureAwait(false);
            Guid? reg = null;
            if (Guid.TryParse(dto.EffectiveRegisterId, out var rid) && rid != Guid.Empty)
                reg = rid;

            var (resultLabel, status) = ClassifyEnsureReady(dto);
            var payload = new
            {
                schema = Schema,
                actionKind = "ensure_ready",
                result = resultLabel,
                errorCode = string.IsNullOrWhiteSpace(dto.MessageCode) ? null : dto.MessageCode.Trim(),
                registerId = reg,
                nextAction = dto.NextAction,
                registerStatus = dto.RegisterStatus,
                resolution = dto.Resolution,
                autoOpened = dto.AutoOpened,
            };

            await _audit.LogPaymentOperationAsync(
                AuditLogActions.POS_REG_READY,
                AuditLogEntityTypes.POS_CRITICAL,
                reg,
                userId,
                role,
                amount: null,
                paymentMethod: null,
                correlationId: null,
                requestData: null,
                responseData: payload,
                description: "POS cash-register ensure-ready outcome",
                status: status,
                errorDetails: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PosCriticalActionAudit: ensure-ready log failed for user {UserId}", userId);
        }
    }

    public async Task LogSpecialReceiptOutcomeAsync(
        string userId,
        Guid cashRegisterId,
        string receiptKind,
        string result,
        AuditLogStatus status,
        string? errorCode = null,
        Guid? paymentId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await ResolveUserRoleAsync(userId).ConfigureAwait(false);
            var payload = new
            {
                schema = Schema,
                actionKind = "special_receipt",
                receiptKind,
                result,
                errorCode,
                registerId = cashRegisterId,
            };

            await _audit.LogPaymentOperationAsync(
                AuditLogActions.POS_SPL_RCPT,
                AuditLogEntityTypes.POS_CRITICAL,
                paymentId,
                userId,
                role,
                amount: null,
                paymentMethod: null,
                correlationId: null,
                requestData: null,
                responseData: payload,
                description: $"RKSV special receipt: {receiptKind}",
                status: status,
                errorDetails: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PosCriticalActionAudit: special receipt log failed for user {UserId}", userId);
        }
    }

    private async Task<string> ResolveUserRoleAsync(string userId)
    {
        var user = await _users.GetUserByIdAsync(userId).ConfigureAwait(false);
        return user?.Role ?? "Unknown";
    }

    private static (string resultLabel, AuditLogStatus status) ClassifyPaymentResult(PaymentResult r)
    {
        if (r.Success)
            return ("success", AuditLogStatus.Success);

        if (IsPolicyBlockedPayment(r))
            return ("blocked", AuditLogStatus.ValidationError);

        return ("failed", AuditLogStatus.Failed);
    }

    private static bool IsPolicyBlockedPayment(PaymentResult r)
    {
        var c = (r.DiagnosticCode ?? "").Trim();
        if (string.IsNullOrEmpty(c))
            return r.IsDeterministicFailure;

        if (c.StartsWith("CASH_REGISTER_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (c.StartsWith("RKSV_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(c, RksvGuardErrorCodes.VoucherCodeRequired, StringComparison.OrdinalIgnoreCase))
            return true;
        if (c.StartsWith("DEMO_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(c, CashRegisterResolutionCodes.Forbidden, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(c, "BENEFIT_DAILY_ALLOWANCE_CONFLICT", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static (string resultLabel, AuditLogStatus status) ClassifyEnsureReady(PosCashRegisterContextDto dto)
    {
        var na = (dto.NextAction ?? "").Trim().ToLowerInvariant();
        if (na == "ready")
            return ("success", AuditLogStatus.Success);

        if (na is "startbeleg_required" or "monatsbeleg_required" or "forbidden" or "select_register" or "open_register")
            return ("blocked", AuditLogStatus.ValidationError);

        return ("failed", AuditLogStatus.Failed);
    }

    private static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s[..(max - 3)] + "...";
    }
}
