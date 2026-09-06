namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Operational Hot / Warm age windows. Cold is everything older than <see cref="WarmDays"/>.
/// Legal 7-year retention is separate (<see cref="BackupRetentionPolicySettings.ColdRetentionYears"/>).
/// </summary>
public readonly record struct BackupRetentionWindows(int HotDays, int WarmDays)
{
    public const int DefaultHotDays = 30;
    public const int DefaultWarmDays = 90;
    public const int MinHotDays = 7;
    public const int MaxHotDays = 90;
    public const int MinWarmDays = 30;
    public const int MaxWarmDays = 365;

    public static BackupRetentionWindows Defaults { get; } = new(DefaultHotDays, DefaultWarmDays);

    public static BackupRetentionWindows FromPolicy(int hotDays, int warmDays)
    {
        var hot = Math.Clamp(hotDays, MinHotDays, MaxHotDays);
        var warm = Math.Clamp(warmDays, MinWarmDays, MaxWarmDays);
        if (warm < hot)
            warm = hot;
        return new BackupRetentionWindows(hot, warm);
    }
}
