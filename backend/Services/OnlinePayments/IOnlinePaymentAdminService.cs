using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.OnlinePayments;

public interface IOnlinePaymentAdminService
{
    Task<AdminOnlinePaymentListResponse> ListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<AdminOnlinePaymentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<AdminOnlinePaymentTestResponse> RunTestAsync(
        AdminOnlinePaymentTestRequest request,
        CancellationToken ct = default);
}
