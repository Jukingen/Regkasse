namespace KasseAPI_Final.Models;

/// <summary>Lifecycle status for TSE signing certificate (API / UI), distinct from device health.</summary>
public enum TseCertLifecycleStatus
{
    Valid = 0,
    /// <summary>Expires within the configured warning window (default 30 days).</summary>
    ExpiringSoon = 1,
    Expired = 2,
    Revoked = 3,
    Invalid = 4,
}
