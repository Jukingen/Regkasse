using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.DigitalServices;

public interface IDigitalServiceRequestService
{
    Task<DigitalServiceRequestResponseDto> CreateAsync(
        Guid tenantId,
        string serviceType,
        string? note,
        string? requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DigitalServiceRequestDto>> ListAsync(
        string? status = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<DigitalServiceRequestResponseDto> ApproveAsync(
        Guid requestId,
        string? resolvedByUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<DigitalServiceRequestResponseDto> RejectAsync(
        Guid requestId,
        string? resolvedByUserId,
        string? note,
        CancellationToken cancellationToken = default);
}
