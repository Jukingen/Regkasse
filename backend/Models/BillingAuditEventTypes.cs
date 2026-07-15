namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>billing_audit_log.action</c> values (non-fiscal Super Admin billing audit).</summary>
public static class BillingAuditEventTypes
{
    public const string SaleCreated = "SALE_CREATED";
    public const string SaleCancelled = "SALE_CANCELLED";
    public const string SaleRefunded = "SALE_REFUNDED";
    public const string LicenseActivated = "LICENSE_ACTIVATED";
    public const string LicenseExtended = "LICENSE_EXTENDED";
    public const string LicenseReminderSent = "LICENSE_REMINDER_SENT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SaleCreated,
        SaleCancelled,
        SaleRefunded,
        LicenseActivated,
        LicenseExtended,
        LicenseReminderSent,
    };
}
