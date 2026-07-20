namespace KasseAPI_Final.Services.License;

/// <summary>Stable restriction codes for mandant license status (clients localize labels).</summary>
public static class LicenseStatusRestrictionCodes
{
    public const string PosOperational = "POS_OPERATIONAL";
    public const string WarningsActive = "WARNINGS_ACTIVE";
    public const string RenewalRecommended = "RENEWAL_RECOMMENDED";
    public const string LockPending = "LOCK_PENDING";
    public const string PosLocked = "POS_LOCKED";
    public const string SuperAdminUnlockOnly = "SUPERADMIN_UNLOCK_ONLY";
    public const string RenewalRequired = "RENEWAL_REQUIRED";
}
