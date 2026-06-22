namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>billing_audit_log.event_type</c> values (non-fiscal Super Admin billing audit).</summary>
public static class BillingAuditEventTypes
{
    public const string LicenseSold = "license_sold";
    public const string LicenseCancelled = "license_cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LicenseSold,
        LicenseCancelled,
    };
}
