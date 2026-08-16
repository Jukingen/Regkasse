using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Cashier-facing TSE indicator for POS (<c>GET /api/pos/tse/status</c>).</summary>
public interface IPosTseStatusService
{
    Task<PosTseStatusDto> GetStatusAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);
}
