using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public sealed record PaymentHistoryActorContext(
    string UserId,
    string? UserRole,
    bool CanCancel,
    bool CanRefund);

public sealed record PaymentHistoryReversalState(
    bool HasStornoChild,
    bool HasRefundChild,
    decimal RefundedAmount);

public interface IPaymentHistoryService
{
    Task<(PaymentHistoryResponse? Response, string? ErrorCode, string? ErrorMessage)> GetRecentPaymentsAsync(
        PaymentHistoryActorContext actor,
        Guid? cashRegisterId,
        int hours = 24,
        string language = "de",
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
