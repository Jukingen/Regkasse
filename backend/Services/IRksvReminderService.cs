using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Unified RKSV Startbeleg / Monatsbeleg / Jahresbeleg reminder status.</summary>
public interface IRksvReminderService
{
    /// <summary>Returns null if the cash register does not exist for the effective tenant.</summary>
    Task<RksvReminderStatusDto?> GetRksvStatusAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
}
