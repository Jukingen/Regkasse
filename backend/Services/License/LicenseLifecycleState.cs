namespace KasseAPI_Final.Services.License;

/// <summary>
/// Mandant license + customer-data lifecycle for expired-license data management (RKSV-aware).
/// Day thresholds: Grace ≤7, Locked 8–30, Archived &gt;30 (see <see cref="Configuration.LicenseGracePeriodConfig"/>).
/// </summary>
public enum LicenseLifecycleState
{
    Active,
    Grace,
    Locked,
    Archived,
    ExportRequest,
    Deleted,
}
