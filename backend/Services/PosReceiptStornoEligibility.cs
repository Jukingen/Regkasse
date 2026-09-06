using KasseAPI_Final.Authorization;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS Belegliste / payment-history storno gates for Cashier (own receipt, Vienna calendar day).
/// Manager and SuperAdmin are not restricted here — FA remains the path for older receipts.
/// </summary>
public static class PosReceiptStornoEligibility
{
    public const string DefaultReason = "Storno aus Belegliste";
    public const string NotOwnerDiagnostic = "STORNO_NOT_OWNER";
    public const string NotTodayDiagnostic = "STORNO_NOT_TODAY";
    public const string NotOwnerErrorKey = "errors.stornoNotOwnReceipt";
    public const string NotTodayErrorKey = "errors.stornoTimeLimitExceeded";

    public static bool IsPrivilegedPosActor(string? role) =>
        string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Roles.Manager, StringComparison.OrdinalIgnoreCase);

    public static bool IsCashier(string? role) =>
        string.Equals(role, Roles.Cashier, StringComparison.OrdinalIgnoreCase);

    public static bool IsOwnReceipt(string? receiptCashierId, string actorUserId) =>
        !string.IsNullOrWhiteSpace(receiptCashierId)
        && !string.IsNullOrWhiteSpace(actorUserId)
        && string.Equals(receiptCashierId.Trim(), actorUserId.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsViennaCalendarToday(DateTime issuedAtUtc, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var issuedDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(issuedAtUtc);
        var today = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(now);
        return issuedDay == today;
    }

    /// <summary>
    /// Resolves a cancellation reason: trimmed user text, or <see cref="DefaultReason"/> when empty.
    /// Returns null when the typed text is shorter than 5 characters.
    /// </summary>
    public static string? ResolveReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return DefaultReason;
        if (trimmed.Length < 5)
            return null;
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}
