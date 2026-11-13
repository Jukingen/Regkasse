namespace KasseAPI_Final.Models;

/// <summary>How a <see cref="DailyClosing"/> was initiated (<see cref="DailyClosing.Trigger"/>).</summary>
public static class DailyClosingTriggers
{
    public const string Manual = "Manual";
    public const string Automatic = "Automatic";

    public static bool IsAutomatic(string? value) =>
        string.Equals(value, Automatic, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        IsAutomatic(value) ? Automatic : Manual;
}

/// <summary>What automatic Tagesabschluss does when unpaid table orders / active carts still exist.</summary>
public static class AutoTagesabschlussOpenOrdersPolicies
{
    public const string NotifyAndContinue = "NotifyAndContinue";
    public const string Block = "Block";
    public const string ForceWithWarning = "ForceWithWarning";

    public static string Normalize(string? value) =>
        value switch
        {
            Block => Block,
            NotifyAndContinue => NotifyAndContinue,
            _ => ForceWithWarning,
        };
}
