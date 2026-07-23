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
    /// <summary>Failed login. Backing value 14 (13 unused — preserved for existing stored enums).</summary>
    LoginFailed = 14,
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
    /// <summary>Custom Identity role created (Super Admin).</summary>
    RoleCreated = 30,
    /// <summary>Permission config snapshot restored.</summary>
    PermissionConfigBackupRestored = 31,
    /// <summary>Permission config snapshot created.</summary>
    PermissionConfigBackupCreated = 32,
    /// <summary>Generic admin file download (history / exports).</summary>
    FileDownloaded = 33,
    /// <summary>System backup artifact downloaded (sensitive; may require 2FA + approval).</summary>
    SystemBackupDownloaded = 34,
    /// <summary>Audit log export file downloaded (sensitive; may require 2FA + approval).</summary>
    AuditLogExportDownloaded = 35,
    /// <summary>GDPR / tenant data-rights ZIP downloaded (sensitive; may require approval).</summary>
    GdprDataExportDownloaded = 36,
    /// <summary>Sensitive export download approval requested.</summary>
    SensitiveExportApprovalRequested = 37,
    /// <summary>Sensitive export download approval granted by Super Admin.</summary>
    SensitiveExportApprovalApproved = 38,
    /// <summary>Sensitive export download approval rejected by Super Admin.</summary>
    SensitiveExportApprovalRejected = 39,
    /// <summary>Admin undid a reversible operation from the operation log.</summary>
    OperationUndone = 40,
    Other = 99
}
