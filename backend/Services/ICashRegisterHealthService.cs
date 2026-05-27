using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;

namespace KasseAPI_Final.Services;

/// <summary>TSE health, offline queue, and sync telemetry for cash register admin surfaces.</summary>
public interface ICashRegisterHealthService
{
    string MapTseHealthStatus(TseHealthSnapshot snapshot, bool tseConfigured);

    Task<CashRegisterTseHealthDto?> GetTseHealthForRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task ApplyOperationalFieldsAsync(
        IReadOnlyList<CashRegisterDto> dtos,
        IReadOnlyList<CashRegister> entities,
        CancellationToken cancellationToken = default);
}
