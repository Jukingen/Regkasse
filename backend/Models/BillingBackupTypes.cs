namespace KasseAPI_Final.Models;

public static class BillingBackupTypes
{
    public const string Sale = "sale";
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Full = "full";

    public static bool IsValid(string? value) =>
        value is Sale or Daily or Weekly or Full;
}

public static class BillingBackupStatuses
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Partial = "partial";

    public static bool IsValid(string? value) =>
        value is Success or Failed or Partial;
}
