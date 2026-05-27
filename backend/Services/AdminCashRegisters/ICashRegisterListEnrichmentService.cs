using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>Enriches admin cash register DTOs with operational telemetry (TSE, offline queue, sync).</summary>
public interface ICashRegisterListEnrichmentService
{
    Task ApplyAsync(
        IReadOnlyList<CashRegisterDto> dtos,
        IReadOnlyList<CashRegister> entities,
        CancellationToken cancellationToken = default);

    Task<CashRegisterTseHealthDto?> GetTseHealthAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
