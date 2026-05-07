using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Monatsbeleg compliance reminder (Vienna month) for cash registers.</summary>
public interface IMonatsbelegReminderService
{
    /// <summary>
    /// Returns null when the cash register does not exist for the effective tenant.
    /// </summary>
    Task<MonatsbelegStatusDto?> GetMonatsbelegStatusAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
