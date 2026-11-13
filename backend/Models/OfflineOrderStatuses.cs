namespace KasseAPI_Final.Models;

public static class OfflineOrderStatuses
{
    public const string Pending = "pending";
    public const string Synced = "synced";
    public const string Failed = "failed";
    public const string Expired = "expired";

    public static bool IsValid(string? value) =>
        value is Pending or Synced or Failed or Expired;
}
