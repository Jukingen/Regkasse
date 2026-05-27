using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>Batch enrichment for admin cash register list/detail rows.</summary>
public sealed class CashRegisterListEnrichmentService : ICashRegisterListEnrichmentService
{
    private readonly ICashRegisterHealthService _health;

    public CashRegisterListEnrichmentService(ICashRegisterHealthService health)
    {
        _health = health;
    }

    public Task ApplyAsync(
        IReadOnlyList<CashRegisterDto> dtos,
        IReadOnlyList<CashRegister> entities,
        CancellationToken cancellationToken = default) =>
        _health.ApplyOperationalFieldsAsync(dtos, entities, cancellationToken);

    public Task<CashRegisterTseHealthDto?> GetTseHealthAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default) =>
        _health.GetTseHealthForRegisterAsync(cashRegisterId, cancellationToken);
}
