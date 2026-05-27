using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public interface IUserUsernameHistoryService
{
    Task RecordChangeAsync(
        string userId,
        string? oldUsername,
        string newUsername,
        string? changedByUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserUsernameHistoryDto>> ListForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Current login name plus all names from history (distinct, case-insensitive).</summary>
    Task<IReadOnlyList<string>> GetKnownUsernamesForUserAsync(
        string userId,
        string? currentUsername,
        CancellationToken cancellationToken = default);
}
