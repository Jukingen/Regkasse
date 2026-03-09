namespace KasseAPI_Final.Services;

/// <summary>
/// Centralized validation for unique user fields (email, employee number, tax number).
/// Create: use IsXxxTakenByOtherUserAsync(..., excludeUserId: null) — any existing value is a conflict.
/// Update: use ValidateUniquenessForUpdateAsync so the current user is excluded and only another user's value is a conflict (self-update safe).
/// </summary>
public interface IUserUniquenessValidationService
{
    /// <summary>True if another user (or any user when excludeUserId is null) has this email.</summary>
    Task<bool> IsEmailTakenByOtherUserAsync(string? email, string? excludeUserId);

    /// <summary>True if another user (or any user when excludeUserId is null) has this employee number. Values are trimmed.</summary>
    Task<bool> IsEmployeeNumberTakenByOtherUserAsync(string? employeeNumber, string? excludeUserId);

    /// <summary>True if another user (or any user when excludeUserId is null) has this tax number. Values are trimmed.</summary>
    Task<bool> IsTaxNumberTakenByOtherUserAsync(string? taxNumber, string? excludeUserId);

    /// <summary>
    /// Validates uniqueness for update. Only reports conflict when a *different* user has the value (current user excluded; null/empty ignored).
    /// Returns (hasConflict, message). Use loaded entity's Id for currentUserId so route/DB casing does not cause false conflict.
    /// </summary>
    Task<(bool HasConflict, string? Message)> ValidateUniquenessForUpdateAsync(
        string currentUserId,
        string? currentEmail,
        string? currentEmployeeNumber,
        string? currentTaxNumber,
        string? newEmail,
        string? newEmployeeNumber,
        string? newTaxNumber);
}
