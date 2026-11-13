namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant policy for POS sales when the previous Vienna-month Monatsbeleg is missing.
/// RKSV requires creating the Monatsbeleg within 7 days of month end; blocking sales is a product policy.
/// </summary>
public enum MonatsbelegBlockingMode
{
    /// <summary>Block shift open and sales until the previous-month Monatsbeleg exists (default).</summary>
    Strict = 0,

    /// <summary>
    /// Allow sales for the first 14 Vienna calendar days of the new month (red warning days 1–7, yellow days 8–14);
    /// block again from day 15.
    /// </summary>
    GracePeriod = 1,

    /// <summary>Never block sales; always warn while the previous-month Monatsbeleg is missing.</summary>
    WarningOnly = 2,
}

/// <summary>Persisted string values for <see cref="CompanySettings.MonatsbelegBlockingMode"/>.</summary>
public static class MonatsbelegBlockingModeNames
{
    public const string Strict = "Strict";
    public const string GracePeriod = "GracePeriod";
    public const string WarningOnly = "WarningOnly";

    public static MonatsbelegBlockingMode Parse(string? value)
    {
        TryParse(value, out var mode);
        return mode;
    }

    public static bool TryParse(string? value, out MonatsbelegBlockingMode mode)
    {
        if (string.Equals(value, GracePeriod, StringComparison.OrdinalIgnoreCase))
        {
            mode = MonatsbelegBlockingMode.GracePeriod;
            return true;
        }

        if (string.Equals(value, WarningOnly, StringComparison.OrdinalIgnoreCase))
        {
            mode = MonatsbelegBlockingMode.WarningOnly;
            return true;
        }

        if (string.Equals(value, Strict, StringComparison.OrdinalIgnoreCase))
        {
            mode = MonatsbelegBlockingMode.Strict;
            return true;
        }

        mode = MonatsbelegBlockingMode.Strict;
        return false;
    }

    public static string ToPersisted(MonatsbelegBlockingMode mode) =>
        mode switch
        {
            MonatsbelegBlockingMode.GracePeriod => GracePeriod,
            MonatsbelegBlockingMode.WarningOnly => WarningOnly,
            _ => Strict,
        };

    public static bool TryNormalize(string? value, out string normalized)
    {
        if (TryParse(value, out var mode))
        {
            normalized = ToPersisted(mode);
            return true;
        }

        normalized = Strict;
        return false;
    }
}
