using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>High-risk payment reversal approval requirement (German UX reasons for POS/admin).</summary>
public sealed class ApprovalRequirement
{
    public bool RequiresApproval { get; init; }

    /// <summary>Primary German reason (first risk factor).</summary>
    public string? Reason { get; init; }

    public IReadOnlyList<string> RiskFactors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

public interface IApprovalWorkflowService
{
    /// <summary>
    /// Evaluates whether a cancel/refund requires manager approval.
    /// Token generation and verification are handled by <see cref="IPaymentReversalApprovalService"/>.
    /// </summary>
    Task<ApprovalRequirement> CheckApprovalRequirementAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal amount,
        string userId,
        CancellationToken cancellationToken = default);
}
