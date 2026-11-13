using KasseAPI_Final.Models.Enums;

namespace KasseAPI_Final.Models;

/// <summary>Tenant lifecycle status stored in <see cref="Tenant.Status"/> (lowercase strings).</summary>
public static class TenantStatuses
{
    public const string Lead = "lead";
    public const string InOnboarding = "in_onboarding";
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";

    /// <summary>Legacy soft-delete value. Prefer <see cref="Archived"/> / <see cref="Cancelled"/> for new writes.</summary>
    public const string Deleted = "deleted";

    /// <summary>Statuses excluded from default Super Admin lists (unless includeDeleted).</summary>
    public static readonly string[] RemovedStatuses = [Deleted, Cancelled, Archived];

    /// <summary>
    /// In-memory comparisons (case-insensitive). Do not use inside EF LINQ — use
    /// <c>!RemovedStatuses.Contains(t.Status)</c> (or explicit OR) instead.
    /// </summary>
    public static bool IsKnown(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var normalized = Normalize(status);
        return string.Equals(normalized, Lead, StringComparison.Ordinal)
            || string.Equals(normalized, InOnboarding, StringComparison.Ordinal)
            || string.Equals(normalized, Active, StringComparison.Ordinal)
            || string.Equals(normalized, Suspended, StringComparison.Ordinal)
            || string.Equals(normalized, Cancelled, StringComparison.Ordinal)
            || string.Equals(normalized, Archived, StringComparison.Ordinal)
            || string.Equals(normalized, Deleted, StringComparison.Ordinal);
    }

    /// <summary>Soft-deleted / cancelled / archived (including legacy <see cref="Deleted"/>).</summary>
    public static bool IsRemoved(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var s = status.Trim().ToLowerInvariant();
        return s is Deleted or Cancelled or Archived;
    }

    /// <summary>
    /// Normalizes API/filter input to storage form. Accepts enum names (e.g. InOnboarding)
    /// and storage strings; maps legacy <c>deleted</c> to <see cref="Archived"/>.
    /// </summary>
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return string.Empty;

        var raw = status.Trim();
        if (Enum.TryParse<TenantStatus>(raw, ignoreCase: true, out var parsed))
            return ToStorage(parsed);

        var lower = raw.ToLowerInvariant().Replace('-', '_');
        return lower switch
        {
            "inonboarding" => InOnboarding,
            "in_onboarding" => InOnboarding,
            "deleted" => Archived,
            _ => lower,
        };
    }

    public static string ToStorage(TenantStatus status) => status switch
    {
        TenantStatus.Lead => Lead,
        TenantStatus.InOnboarding => InOnboarding,
        TenantStatus.Active => Active,
        TenantStatus.Suspended => Suspended,
        TenantStatus.Cancelled => Cancelled,
        TenantStatus.Archived => Archived,
        _ => Active,
    };

    /// <summary>Maps DB string (including legacy <c>deleted</c>) to <see cref="TenantStatus"/>.</summary>
    public static TenantStatus? TryParse(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var normalized = Normalize(status);
        return normalized switch
        {
            Lead => TenantStatus.Lead,
            InOnboarding => TenantStatus.InOnboarding,
            Active => TenantStatus.Active,
            Suspended => TenantStatus.Suspended,
            Cancelled => TenantStatus.Cancelled,
            Archived => TenantStatus.Archived,
            Deleted => TenantStatus.Archived,
            _ => null,
        };
    }

    public static bool TryParse(string? status, out TenantStatus value)
    {
        var parsed = TryParse(status);
        if (parsed is null)
        {
            value = default;
            return false;
        }

        value = parsed.Value;
        return true;
    }
}
