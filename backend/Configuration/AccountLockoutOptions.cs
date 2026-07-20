namespace KasseAPI_Final.Configuration;

/// <summary>
/// In-memory login attempt lockout (identifier-scoped). Complements Identity user flags;
/// does not replace ASP.NET Identity <c>LockoutEnd</c> used by admin reactivation.
/// </summary>
public sealed class AccountLockoutOptions
{
    public const string SectionName = "AccountLockout";

    /// <summary>When false, lockout checks and attempt recording are no-ops.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Failed credential attempts before lockout.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Lockout and attempt-window duration in minutes.</summary>
    public int LockoutMinutes { get; set; } = 15;
}
