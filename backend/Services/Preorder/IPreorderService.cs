using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Preorder;

public interface IPreorderService
{
    Task TryCreateFromSuccessfulPaymentAsync(
        PaymentDetails payment,
        CreatePaymentRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task MarkCancelledForPaymentAsync(
        Guid sourcePaymentId,
        CancellationToken cancellationToken = default);

    Task<PreorderBalanceGuardResult> ValidateBalancePaymentAsync(
        Guid orderId,
        decimal paymentAmount,
        CancellationToken cancellationToken = default);

    Task ApplyBalancePaymentAsync(
        Guid orderId,
        PaymentDetails payment,
        CancellationToken cancellationToken = default);

    Task<PreorderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PreorderDto?> GetByReceiptNumberAsync(
        string receiptNumber,
        CancellationToken cancellationToken = default);

    Task<PreorderListResponseDto> ListAsync(
        string? status,
        string? receiptNumber,
        int take,
        CancellationToken cancellationToken = default);

    Task<PreorderStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<PreorderDto?> SetOperationalStatusAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken = default);

    Task<PreorderSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<PreorderSettingsDto> UpdateSettingsAsync(
        PreorderSettingsDto request,
        CancellationToken cancellationToken = default);
}
