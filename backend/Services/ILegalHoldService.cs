using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Sprint 5: Legal hold on audit date ranges; cleanup skips records within active holds.</summary>
public interface ILegalHoldService
{
    Task<LegalHold> CreateAsync(DateTime fromDate, DateTime toDate, string? reason, string? createdBy);
    Task<IEnumerable<LegalHold>> GetActiveAsync();
    Task<IEnumerable<LegalHold>> GetAllAsync(bool activeOnly = true);
    Task<LegalHold?> GetByIdAsync(Guid id);
    Task<bool> DeactivateAsync(Guid id);
}
