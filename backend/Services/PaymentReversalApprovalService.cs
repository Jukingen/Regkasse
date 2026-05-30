namespace KasseAPI_Final.Services;

using KasseAPI_Final.Models;

public sealed class PaymentReversalApprovalException : Exception
{
    public string Code { get; }

    public PaymentReversalApprovalException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public enum PaymentReversalApprovalGateResult
{
    NotRequired,
    Approved,
    ApprovalRequired,
    InvalidToken
}

public sealed class PaymentReversalApprovalGateOutcome
{
    public PaymentReversalApprovalGateResult Result { get; init; }
    public Guid? ApprovalRequestId { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool NotificationSent { get; init; }
    public IReadOnlyList<string> RiskFactors { get; init; } = Array.Empty<string>();
}

public interface IPaymentReversalApprovalService
{
    Task<Models.DTOs.PaymentReversalPolicyDto> AssessPolicyAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string? requestedByUserId = null,
        CancellationToken cancellationToken = default);

    Task<PaymentReversalApprovalGateOutcome> EnforceApprovalAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        string reason,
        int reasonCode,
        string requestedByUserId,
        string? approvalToken,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
