using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Helpers;

/// <summary>
/// Allocates memorable login usernames: {rolePrefix}{n} (e.g. manager1, user2).
/// Retries with a random suffix when the candidate is already taken.
/// </summary>
public static class UniqueUsernameGenerator
{
    public const int AllocationMaxAttempts = 8;

    public const int AvailableNumbersPreviewCount = 3;

    public static string GetRolePrefix(string? role) =>
        role?.Trim().ToLowerInvariant() switch
        {
            "superadmin" => "admin",
            "manager" => "manager",
            "cashier" => "cashier",
            _ => "user",
        };

    public static async Task<string> AllocateUniqueUsernameAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        string role,
        CancellationToken cancellationToken = default)
    {
        var baseName = await GenerateNextUsernameAsync(db, role, cancellationToken).ConfigureAwait(false);

        for (var attempt = 0; attempt < AllocationMaxAttempts; attempt++)
        {
            var candidate = attempt == 0
                ? baseName
                : $"{baseName}_{QuickUserEmailGenerator.GenerateSuffix()}";

            if (await IsUsernameAvailableAsync(userManager, candidate).ConfigureAwait(false))
                return candidate;
        }

        return $"{baseName}_{QuickUserEmailGenerator.GenerateSuffix()}";
    }

    public static async Task<string> GenerateNextUsernameAsync(
        AppDbContext db,
        string role,
        CancellationToken cancellationToken = default)
    {
        var prefix = GetRolePrefix(role);
        var names = await db.Users
            .AsNoTracking()
            .Where(u => u.UserName != null && u.UserName.ToLower().StartsWith(prefix))
            .Select(u => u.UserName!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var maxNumber = names
            .Select(name => ParseNumericSuffix(name, prefix))
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxNumber + 1}";
    }

    /// <summary>
    /// Suggests the next incremental username for a role and lists the next free numeric suffixes
    /// (aligned with <see cref="GenerateNextUsernameAsync"/>).
    /// </summary>
    public static async Task<(string SuggestedUsername, IReadOnlyList<int> AvailableNumbers)> GetSuggestionAsync(
        AppDbContext db,
        string role,
        CancellationToken cancellationToken = default)
    {
        var prefix = GetRolePrefix(role);
        var names = await db.Users
            .AsNoTracking()
            .Where(u => u.UserName != null && u.UserName.ToLower().StartsWith(prefix))
            .Select(u => u.UserName!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var takenNumbers = names
            .Select(name => ParseNumericSuffix(name, prefix))
            .Where(n => n > 0)
            .ToHashSet();

        var suggestedUsername = await GenerateNextUsernameAsync(db, role, cancellationToken).ConfigureAwait(false);
        var startNumber = ParseNumericSuffix(suggestedUsername, prefix);
        if (startNumber <= 0)
            startNumber = 1;

        var availableNumbers = new List<int>(AvailableNumbersPreviewCount);
        for (var n = startNumber; availableNumbers.Count < AvailableNumbersPreviewCount; n++)
        {
            if (!takenNumbers.Contains(n))
                availableNumbers.Add(n);
        }

        return (suggestedUsername, availableNumbers);
    }

    internal static int ParseNumericSuffix(string userName, string prefix)
    {
        if (!userName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return 0;

        var suffix = userName[prefix.Length..];
        var digitLength = 0;
        while (digitLength < suffix.Length && char.IsDigit(suffix[digitLength]))
            digitLength++;

        if (digitLength == 0)
            return 0;

        return int.TryParse(suffix[..digitLength], out var num) ? num : 0;
    }

    private static async Task<bool> IsUsernameAvailableAsync(
        UserManager<ApplicationUser> userManager,
        string userName)
    {
        var existing = await IdentityLoginLookup.FindByUserNameIgnoreCaseAsync(userManager, userName)
            .ConfigureAwait(false);
        return existing == null;
    }
}
