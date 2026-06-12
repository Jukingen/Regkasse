using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PaymentHistoryService : IPaymentHistoryService
{
    private const int MaxLimit = 100;
    private const decimal RefundToleranceEur = 0.01m;

    private readonly AppDbContext _context;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IUserService _userService;
    private readonly IPaymentReversalApprovalService _reversalApproval;
    private readonly ILogger<PaymentHistoryService> _logger;

    public PaymentHistoryService(
        AppDbContext context,
        ICurrentTenantAccessor tenantAccessor,
        ISettingsTenantResolver tenantResolver,
        IUserService userService,
        IPaymentReversalApprovalService reversalApproval,
        ILogger<PaymentHistoryService> logger)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
        _tenantResolver = tenantResolver;
        _userService = userService;
        _reversalApproval = reversalApproval;
        _logger = logger;
    }

    public async Task<(PaymentHistoryResponse? Response, string? ErrorCode, string? ErrorMessage)> GetRecentPaymentsAsync(
        PaymentHistoryActorContext actor,
        Guid? cashRegisterId,
        int hours = 24,
        string language = "de",
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours <= 0 ? 24 : hours, 1, 168);
        limit = Math.Clamp(limit <= 0 ? 20 : limit, 1, MaxLimit);
        offset = Math.Max(0, offset);
        language = NormalizeLanguage(language);

        var registerId = await ResolveCashRegisterIdAsync(actor.UserId, cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (registerId == null)
        {
            return (null, "POS_PAYMENT_HISTORY_NO_REGISTER",
                "Cash register is required. Provide cashRegisterId or start an active shift.");
        }

        var tenantId = _tenantAccessor.TenantId
                       ?? await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

        var registerOk = await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
            .AnyAsync(cr => cr.Id == registerId.Value && cr.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!registerOk)
            return (null, "POS_PAYMENT_HISTORY_REGISTER_NOT_FOUND", "Cash register not found");

        var userRole = actor.UserRole ?? await ResolveUserRoleAsync(actor.UserId, cancellationToken).ConfigureAwait(false);
        var actorWithRole = actor with { UserRole = userRole };

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-hours);

        var baseQuery = _context.PaymentDetails.AsNoTracking()
            .Where(p => p.CashRegisterId == registerId.Value
                        && p.CreatedAt >= fromUtc
                        && p.CreatedAt <= toUtc);

        var totalCount = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var payments = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (payments.Count == 0)
        {
            return (EmptyResponse(registerId.Value, fromUtc, toUtc, language, totalCount, limit, offset), null, null);
        }

        var paymentIds = payments.Select(p => p.Id).ToList();
        var reversalByOriginal = await LoadReversalStateAsync(paymentIds, cancellationToken).ConfigureAwait(false);
        var voucherPaymentIds = await LoadVoucherPaymentIdsAsync(paymentIds, cancellationToken).ConfigureAwait(false);

        var result = new List<PaymentHistoryItemDto>(payments.Count);
        foreach (var payment in payments)
        {
            reversalByOriginal.TryGetValue(payment.Id, out var reversalState);
            reversalState ??= new PaymentHistoryReversalState(false, false, 0m);

            var hasVoucher = voucherPaymentIds.Contains(payment.Id)
                             || payment.PaymentMethod == PaymentMethod.Voucher;

            var policyStornoApproval = false;
            var policyRefundApproval = false;

            if (IsEligibleOriginalSale(payment))
            {
                if (actorWithRole.CanCancel && !reversalState.HasStornoChild && !reversalState.HasRefundChild)
                {
                    var stornoPolicy = await _reversalApproval.AssessPolicyAsync(
                        payment,
                        PaymentReversalOperation.Cancel,
                        null,
                        actorWithRole.UserId,
                        cancellationToken).ConfigureAwait(false);
                    policyStornoApproval = stornoPolicy.RequiresApproval;
                }

                if (CanOfferRefundByRole(actorWithRole.UserRole)
                    && actorWithRole.CanRefund
                    && !reversalState.HasStornoChild
                    && !hasVoucher)
                {
                    var remaining = payment.TotalAmount - reversalState.RefundedAmount;
                    if (remaining > RefundToleranceEur)
                    {
                        var refundPolicy = await _reversalApproval.AssessPolicyAsync(
                            payment,
                            PaymentReversalOperation.Refund,
                            remaining,
                            actorWithRole.UserId,
                            cancellationToken).ConfigureAwait(false);
                        policyRefundApproval = refundPolicy.RequiresApproval;
                    }
                }
            }

            result.Add(new PaymentHistoryItemDto
            {
                Id = payment.Id,
                ReceiptNumber = payment.ReceiptNumber,
                TotalAmount = payment.TotalAmount,
                CreatedAt = payment.CreatedAt,
                PaymentMethod = AdminPaymentListMapper.ParsePaymentMethodName(payment.PaymentMethodRaw),
                CustomerName = string.IsNullOrWhiteSpace(payment.CustomerName) ? "Walk-in" : payment.CustomerName,
                TableNumber = payment.TableNumber > 0 ? payment.TableNumber : null,
                IsStorno = payment.IsStorno,
                IsRefund = payment.IsRefund,
                AvailableActions = GetAvailableActions(
                    payment,
                    reversalState,
                    actorWithRole,
                    hours,
                    hasVoucher,
                    policyStornoApproval,
                    policyRefundApproval),
            });
        }

        _logger.LogDebug(
            "Payment history: tenant={TenantId}, register={RegisterId}, hours={Hours}, lang={Language}, count={Count}/{Total}",
            tenantId,
            registerId.Value,
            hours,
            language,
            result.Count,
            totalCount);

        return (new PaymentHistoryResponse
        {
            Payments = result,
            TotalCount = totalCount,
            Limit = limit,
            Offset = offset,
            HasMore = offset + limit < totalCount,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            CashRegisterId = registerId.Value,
            Language = language,
        }, null, null);
    }

    internal List<AvailableAction> GetAvailableActions(
        PaymentDetails payment,
        PaymentHistoryReversalState reversalState,
        PaymentHistoryActorContext actor,
        int windowHours,
        bool hasVoucherRedemption,
        bool policyStornoApproval,
        bool policyRefundApproval)
    {
        var actions = new List<AvailableAction>();
        var hoursSincePayment = (DateTime.UtcNow - payment.CreatedAt).TotalHours;
        var isManagerRole = CanOfferRefundByRole(actor.UserRole);

        if (actor.CanCancel
            && IsEligibleOriginalSale(payment)
            && !payment.IsStorno
            && !payment.IsRefund
            && !reversalState.HasStornoChild
            && !reversalState.HasRefundChild
            && hoursSincePayment <= windowHours)
        {
            actions.Add(new AvailableAction
            {
                Action = "storno",
                LabelKey = PaymentHistoryLabelKeys.Actions.Storno,
                RequiresReason = true,
                RequiresManagerApproval = !isManagerRole || policyStornoApproval,
                ReasonLabelKey = PaymentHistoryLabelKeys.ReasonFields.StornoTitle,
                ReasonOptions = PaymentHistoryLabelKeys.StornoReasonOptions.ToList(),
            });
        }

        var remaining = payment.TotalAmount - reversalState.RefundedAmount;
        if (actor.CanRefund
            && isManagerRole
            && IsEligibleOriginalSale(payment)
            && !payment.IsRefund
            && !reversalState.HasStornoChild
            && !hasVoucherRedemption
            && payment.PaymentMethod != PaymentMethod.Voucher
            && payment.TotalAmount > 0m
            && remaining > RefundToleranceEur)
        {
            actions.Add(new AvailableAction
            {
                Action = "refund",
                LabelKey = PaymentHistoryLabelKeys.Actions.Refund,
                RequiresReason = true,
                RequiresManagerApproval = policyRefundApproval,
                ReasonLabelKey = PaymentHistoryLabelKeys.ReasonFields.RefundTitle,
                ReasonOptions = PaymentHistoryLabelKeys.RefundReasonOptions.ToList(),
            });
        }

        if (actions.Count == 0)
        {
            actions.Add(new AvailableAction
            {
                Action = "view_only",
                LabelKey = PaymentHistoryLabelKeys.Actions.View,
                RequiresReason = false,
                RequiresManagerApproval = false,
            });
        }

        return actions;
    }

    private static bool IsEligibleOriginalSale(PaymentDetails payment) =>
        payment.IsActive
        && !payment.IsStorno
        && !payment.IsRefund
        && string.IsNullOrWhiteSpace(payment.RksvSpecialReceiptKind)
        && payment.CashRegisterId != Guid.Empty
        && !string.IsNullOrWhiteSpace(payment.ReceiptNumber);

    private static bool CanOfferRefundByRole(string? userRole) =>
        string.Equals(userRole, Roles.Manager, StringComparison.OrdinalIgnoreCase)
        || string.Equals(userRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ResolveUserRoleAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByIdAsync(userId).ConfigureAwait(false);
        return user?.Role;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "de";
        var normalized = language.Trim().ToLowerInvariant();
        return normalized switch
        {
            "de" or "en" or "tr" => normalized,
            _ => "de",
        };
    }

    private static PaymentHistoryResponse EmptyResponse(
        Guid registerId,
        DateTime fromUtc,
        DateTime toUtc,
        string language,
        int totalCount,
        int limit,
        int offset) => new()
    {
        Payments = new List<PaymentHistoryItemDto>(),
        TotalCount = totalCount,
        Limit = limit,
        Offset = offset,
        HasMore = offset + limit < totalCount,
        FromUtc = fromUtc,
        ToUtc = toUtc,
        CashRegisterId = registerId,
        Language = language,
    };

    private async Task<Guid?> ResolveCashRegisterIdAsync(
        string userId,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            return cashRegisterId.Value;

        return await _context.CashierShifts.AsNoTracking()
            .Where(s => s.CashierId == userId
                        && s.Status == CashierShiftStatuses.Active
                        && s.IsActive)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => (Guid?)s.CashRegisterId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, PaymentHistoryReversalState>> LoadReversalStateAsync(
        IReadOnlyList<Guid> originalPaymentIds,
        CancellationToken cancellationToken)
    {
        var children = await _context.PaymentDetails.AsNoTracking()
            .Where(p => p.OriginalPaymentId != null
                        && originalPaymentIds.Contains(p.OriginalPaymentId.Value))
            .Select(p => new
            {
                OriginalId = p.OriginalPaymentId!.Value,
                p.IsStorno,
                p.IsRefund,
                p.IsActive,
                p.TotalAmount,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<Guid, PaymentHistoryReversalState>();
        foreach (var group in children.GroupBy(c => c.OriginalId))
        {
            var hasStorno = group.Any(c => c.IsStorno);
            var refunded = group
                .Where(c => c.IsRefund && c.IsActive)
                .Sum(c => -c.TotalAmount);
            var hasRefund = group.Any(c => c.IsRefund && c.IsActive);
            map[group.Key] = new PaymentHistoryReversalState(hasStorno, hasRefund, refunded);
        }

        return map;
    }

    private async Task<HashSet<Guid>> LoadVoucherPaymentIdsAsync(
        IReadOnlyList<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        var ids = await _context.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.PaymentId != null
                        && paymentIds.Contains(l.PaymentId.Value)
                        && l.Type == VoucherTransactionType.Redeem)
            .Select(l => l.PaymentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }
}
