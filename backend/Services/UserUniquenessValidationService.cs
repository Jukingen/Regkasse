using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Validates uniqueness of email, employee number, and tax number using the same exclude-by-user-id logic
/// so that update flows do not treat the current user's own record as a conflict.
/// </summary>
public class UserUniquenessValidationService : IUserUniquenessValidationService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserUniquenessValidationService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<bool> IsEmailTakenByOtherUserAsync(string? email, string? excludeUserId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing == null)
            return false;
        // Exclude current user: use ordinal-ignore-case so route vs DB Id casing does not cause false conflict.
        return !string.Equals(existing.Id, excludeUserId, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<bool> IsEmployeeNumberTakenByOtherUserAsync(string? employeeNumber, string? excludeUserId)
    {
        var value = employeeNumber?.Trim();
        if (string.IsNullOrEmpty(value))
            return false;
        var existing = await _userManager.Users
            .Where(u => u.EmployeeNumber == value && u.IsActive)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();
        if (existing == null)
            return false;
        // Exclude current user: compare in C# with OrdinalIgnoreCase so excludeUserId (must be loaded entity.Id, not route id) matches.
        return !string.Equals(existing.Id, excludeUserId, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<bool> IsTaxNumberTakenByOtherUserAsync(string? taxNumber, string? excludeUserId)
    {
        var value = taxNumber?.Trim();
        if (string.IsNullOrEmpty(value))
            return false;
        var existing = await _userManager.Users
            .Where(u => u.TaxNumber == value)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();
        if (existing == null)
            return false;
        return !string.Equals(existing.Id, excludeUserId, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<(bool HasConflict, string? Message)> ValidateUniquenessForUpdateAsync(
        string currentUserId,
        string? currentEmail,
        string? currentEmployeeNumber,
        string? currentTaxNumber,
        string? newEmail,
        string? newEmployeeNumber,
        string? newTaxNumber)
    {
        if (string.IsNullOrEmpty(currentUserId))
            return (false, null);

        var newEmailTrimmed = newEmail?.Trim();
        var newEmpTrimmed = newEmployeeNumber?.Trim();
        var newTaxTrimmed = newTaxNumber?.Trim();
        var curEmailTrimmed = currentEmail?.Trim();
        var curEmpTrimmed = currentEmployeeNumber?.Trim();
        var curTaxTrimmed = currentTaxNumber?.Trim();

        if (!string.IsNullOrEmpty(newEmailTrimmed) && newEmailTrimmed != curEmailTrimmed
            && await IsEmailTakenByOtherUserAsync(newEmailTrimmed, currentUserId))
            return (true, "Email already exists");

        if (!string.IsNullOrEmpty(newEmpTrimmed) && newEmpTrimmed != curEmpTrimmed
            && await IsEmployeeNumberTakenByOtherUserAsync(newEmpTrimmed, currentUserId))
            return (true, "Employee number already exists");

        if (!string.IsNullOrEmpty(newTaxTrimmed) && newTaxTrimmed != curTaxTrimmed
            && await IsTaxNumberTakenByOtherUserAsync(newTaxTrimmed, currentUserId))
            return (true, "Tax number already exists");

        return (false, null);
    }
}
