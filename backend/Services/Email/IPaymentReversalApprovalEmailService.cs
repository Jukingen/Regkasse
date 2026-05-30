using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Email;

public sealed record PaymentReversalApprovalEmailContext(
    Guid PaymentId,
    string? ReceiptNumber,
    decimal Amount,
    PaymentReversalOperation Operation,
    DateTime ExpiresAtUtc,
    string RequestedByUserId);

public interface IPaymentReversalApprovalEmailService
{
    Task<int> TrySendApprovalTokenAsync(
        IReadOnlyList<string> approverEmails,
        string approvalToken,
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal? refundAmount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);
}
