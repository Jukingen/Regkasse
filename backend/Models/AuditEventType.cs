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
    /// <summary>Product catalog price and/or tax group changed (RKSV price version trail).</summary>
    ProductPriceChanged = 41,
    /// <summary>Product superseded by a new catalog version due to prior fiscal sales (RKSV).</summary>
    ProductCatalogVersionCreated = 42,
    /// <summary>RKSV DEP §7 export created (manual or scheduled).</summary>
    RksvDepExportCreated = 43,
    /// <summary>RKSV DEP §7 export downloaded from admin.</summary>
    RksvDepExportDownloaded = 44,
    /// <summary>RKSV DEP §7 export archived for long-term retention.</summary>
    RksvDepExportArchived = 45,
    /// <summary>RKSV DEP §7 archived export purged after retention.</summary>
    RksvDepExportPurged = 46,
    /// <summary>RKSV DEP §7 export validated (manual or automatic).</summary>
    RksvDepExportValidated = 47,
    /// <summary>RKSV DEP §7 export generation failed.</summary>
    RksvDepExportFailed = 48,
    /// <summary>FA license renewal page or modal viewed (funnel analytics; deduped per day).</summary>
    LicenseRenewalPageViewed = 49,
    /// <summary>Operator marked a TSE device compliant for the Mai 2027 Signaturkarte program.</summary>
    SignaturkarteProgramMarkedCompliant = 50,
    /// <summary>Signaturkarte program reminder sweep published activity/email for a tenant.</summary>
    SignaturkarteProgramReminderSent = 51,

    /// <summary>Ausfall / Wiederinbetriebnahme episode created (suggestion or manual).</summary>
    RksvAusfallEpisodeCreated = 52,

    /// <summary>Ausfall / Wiederinbetriebnahme episode enqueued to FinanzOnline outbox.</summary>
    RksvAusfallEpisodeEnqueued = 53,

    /// <summary>Episode closed as completed via FinanzOnline portal (manual mark).</summary>
    RksvAusfallMarkedManualPortal = 54,

    /// <summary>Suggested Ausfall episode cancelled before FON send.</summary>
    RksvAusfallSuggestionCancelled = 55,

    /// <summary>Super Admin changed a feature flag override (global or tenant).</summary>
    FeatureFlagChanged = 56,

    /// <summary>Deployment pipeline started (CI report or manual).</summary>
    DeploymentStarted = 57,

    /// <summary>Deployment completed successfully (incl. smoke when applicable).</summary>
    DeploymentSucceeded = 58,

    /// <summary>Deployment failed (smoke, webhook, or operator abort).</summary>
    DeploymentFailed = 59,

    /// <summary>Deployment rollback invoked (stage or tenant).</summary>
    DeploymentRollback = 60,

    /// <summary>Compliance officer signed off production deployment checklist.</summary>
    DeploymentComplianceApproved = 61,

    Other = 99
}
