using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class PaymentReversalApprovalService : IPaymentReversalApprovalService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IPaymentReversalApprovalEmailService _email;
    private readonly IApprovalWorkflowService _workflow;
    private readonly IOptionsMonitor<PaymentReversalApprovalOptions> _options;
    private readonly ILogger<PaymentReversalApprovalService> _logger;

    public PaymentReversalApprovalService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IPaymentReversalApprovalEmailService email,
        IApprovalWorkflowService workflow,
        IOptionsMonitor<PaymentReversalApprovalOptions> options,
        ILogger<PaymentReversalApprovalService> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _email = email;
        _workflow = workflow;
        _options = options;
        _logger = logger;
    }

    public async Task<PaymentReversalPolicyDto> AssessPolicyAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string? requestedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var amount = ResolveOperationAmount(payment, operation, refundAmount);
        var requirement = await _workflow.CheckApprovalRequirementAsync(
            payment,
            operation,
            amount,
            requestedByUserId ?? string.Empty,
            cancellationToken);

        return new PaymentReversalPolicyDto
        {
            RequiresApproval = requirement.RequiresApproval,
            Operation = operation.ToString(),
            Amount = amount,
            RiskFactors = requirement.RiskFactors,
            Reason = requirement.Reason,
            Reasons = requirement.Reasons,
        };
    }

    public async Task<PaymentReversalApprovalGateOutcome> EnforceApprovalAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string reason,
        int reasonCode,
        string requestedByUserId,
        string? approvalToken,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var policy = await AssessPolicyAsync(
            payment,
            operation,
            refundAmount,
            requestedByUserId,
            cancellationToken);
        if (!policy.RequiresApproval)
            return new PaymentReversalApprovalGateOutcome { Result = PaymentReversalApprovalGateResult.NotRequired };

        if (string.IsNullOrWhiteSpace(approvalToken))
        {
            var created = await CreatePendingApprovalAsync(
                payment,
                operation,
                refundAmount,
                reason,
                reasonCode,
                requestedByUserId,
                idempotencyKey,
                cancellationToken);
            return new PaymentReversalApprovalGateOutcome
            {
                Result = PaymentReversalApprovalGateResult.ApprovalRequired,
                ApprovalRequestId = created.ApprovalRequestId,
                ExpiresAtUtc = created.ExpiresAtUtc,
                NotificationSent = created.NotificationSent,
                RiskFactors = policy.RiskFactors,
            };
        }

        if (!ManualRestoreApprovalTokenHasher.IsValidSixDigitFormat(approvalToken))
        {
            return new PaymentReversalApprovalGateOutcome
            {
                Result = PaymentReversalApprovalGateResult.InvalidToken,
                RiskFactors = policy.RiskFactors,
            };
        }

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var pending = await _db.PaymentReversalApprovals
            .Where(a => a.TenantId == tenantId
                        && a.PaymentId == payment.Id
                        && a.Operation == operation
                        && a.Status == PaymentReversalApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pending == null
            || pending.ApprovalTokenExpiresAtUtc == null
            || pending.ApprovalTokenExpiresAtUtc < DateTime.UtcNow
            || pending.ApprovalTokenHash == null
            || !ManualRestoreApprovalTokenHasher.Verify(approvalToken, pending.ApprovalTokenHash))
        {
            return new PaymentReversalApprovalGateOutcome
            {
                Result = PaymentReversalApprovalGateResult.InvalidToken,
                RiskFactors = policy.RiskFactors,
            };
        }

        if (operation == PaymentReversalOperation.Refund && pending.RefundAmount.HasValue && refundAmount.HasValue
            && Math.Abs(pending.RefundAmount.Value - refundAmount.Value) > 0.01m)
        {
            return new PaymentReversalApprovalGateOutcome
            {
                Result = PaymentReversalApprovalGateResult.InvalidToken,
                RiskFactors = policy.RiskFactors,
            };
        }

        pending.Status = PaymentReversalApprovalStatus.Consumed;
        pending.ConsumedAtUtc = DateTime.UtcNow;
        pending.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new PaymentReversalApprovalGateOutcome { Result = PaymentReversalApprovalGateResult.Approved };
    }

    private async Task<PaymentReversalApprovalRequestDto> CreatePendingApprovalAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string reason,
        int reasonCode,
        string requestedByUserId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var ttl = TimeSpan.FromMinutes(Math.Max(1, _options.CurrentValue.ApprovalTokenTtlMinutes));
        var plainToken = ManualRestoreApprovalTokenHasher.GenerateSixDigitToken();
        var expires = DateTime.UtcNow.Add(ttl);

        var entity = new PaymentReversalApproval
        {
            TenantId = tenantId,
            PaymentId = payment.Id,
            Operation = operation,
            RefundAmount = refundAmount,
            Reason = reason.Trim(),
            ReasonCode = reasonCode,
            Status = PaymentReversalApprovalStatus.Pending,
            ApprovalTokenHash = ManualRestoreApprovalTokenHasher.Hash(plainToken),
            ApprovalTokenExpiresAtUtc = expires,
            RequestedByUserId = requestedByUserId,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.PaymentReversalApprovals.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var approverEmails = await ResolveApproverEmailsAsync(tenantId, requestedByUserId, cancellationToken);
        var sent = await _email.TrySendApprovalTokenAsync(
            approverEmails,
            plainToken,
            payment,
            operation,
            refundAmount,
            expires,
            cancellationToken);

        if (sent == 0)
        {
            _logger.LogWarning(
                "Payment reversal approval token created but no notification sent. ApprovalId={ApprovalId} PaymentId={PaymentId}",
                entity.Id,
                payment.Id);
        }

        return new PaymentReversalApprovalRequestDto
        {
            ApprovalRequestId = entity.Id,
            ExpiresAtUtc = expires,
            NotificationSent = sent > 0,
        };
    }

    private static decimal ResolveOperationAmount(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount) =>
        operation == PaymentReversalOperation.Refund
            ? refundAmount ?? payment.TotalAmount
            : payment.TotalAmount;

    private async Task<IReadOnlyList<string>> ResolveApproverEmailsAsync(
        Guid tenantId,
        string requestedByUserId,
        CancellationToken cancellationToken)
    {
        var managers = await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { u.Email, u.Id, u.Role })
            .Where(x => x.Role == Roles.Manager && x.Id != requestedByUserId && x.Email != null && x.Email != "")
            .Select(x => x.Email!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (managers.Count > 0)
            return managers;

        return _options.CurrentValue.FallbackApproverEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal sealed class NoOpPaymentReversalApprovalService : IPaymentReversalApprovalService
{
    public static readonly NoOpPaymentReversalApprovalService Instance = new();

    public Task<PaymentReversalPolicyDto> AssessPolicyAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string? requestedByUserId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentReversalPolicyDto
        {
            RequiresApproval = false,
            Operation = operation.ToString(),
            Amount = operation == PaymentReversalOperation.Refund ? refundAmount : payment.TotalAmount,
        });

    public Task<PaymentReversalApprovalGateOutcome> EnforceApprovalAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string reason,
        int reasonCode,
        string requestedByUserId,
        string? approvalToken,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentReversalApprovalGateOutcome
        {
            Result = PaymentReversalApprovalGateResult.NotRequired,
        });
}
