namespace KasseAPI_Final.Models;

/// <summary>
/// Standardized audit event types. Every event includes: actor, target, timestamp, actionType.
/// USER_UPDATED must include structured changes; USER_ROLE_CHANGED must include role diff in changes.
/// Backing values preserved for existing logs (safe migration).
/// </summary>
public enum AuditEventType
{
    UserCreated = 0,
    UserUpdated = 1,
    UserRoleChanged = 2,
    UserDeactivated = 3,
    UserReactivated = 4,
    PasswordResetForced = 5,
    ChangeOwnPassword = 6,
    UserPasswordReset = 7,
    RolePermissionsUpdated = 8,
    RoleDeleted = 9,
    LoginSuccess = 10,
    UserLogout = 11,
    UserDeleted = 12,
    LoginFailed = 14,  // New; 11–12 preserved for existing logs
    UserTenantMembershipChanged = 15,
    UserNameChanged = 16,
    /// <summary>Super Admin requested validation-only manual restore (second approval required).</summary>
    RestoreRequested = 17,
    /// <summary>Second Super Admin approved manual restore; validation drill enqueued.</summary>
    RestoreApproved = 18,
    /// <summary>Second Super Admin rejected manual restore request.</summary>
    RestoreRejected = 19,
    /// <summary>Validation-only manual restore completed successfully.</summary>
    RestoreCompleted = 20,
    /// <summary>Validation-only manual restore failed during execution.</summary>
    RestoreFailed = 21,
    CategoryUpdated = 22,
    CategoryDemoReset = 23,
    InvoiceResent = 24,
    UserPermissionOverridesChanged = 25,
    LicenseRenewed = 26,
    LicenseExtended = 27,
    /// <summary>Super Admin or Manager updated mandant license key and/or validity.</summary>
    LicenseUpdated = 28,
    /// <summary>Persisted RKSV report PDF downloaded from admin (Nachdruck / stored copy).</summary>
    ReportPdfDownloaded = 29,
    Other = 99
}
