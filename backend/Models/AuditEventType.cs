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
    /// <summary>System backup artifact downloaded (sensitive; may require approval / privacy ack).</summary>
    SystemBackupDownloaded = 34,
    /// <summary>Audit log export file downloaded (sensitive; may require approval / privacy ack).</summary>
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

    /// <summary>Super Admin manually cleared application cache (troubleshooting).</summary>
    SystemCacheCleared = 62,

    /// <summary>SaaS trial converted to a paid license sale.</summary>
    TrialConvertedToPaid = 63,

    /// <summary>Super Admin changed the Fiskaly SIGN AT enable/disable overlay.</summary>
    FiskalySettingsChanged = 64,

    /// <summary>FON (FinanzOnline) credentials submitted to fiskaly SIGN AT (PIN never logged).</summary>
    FiskalyFonAuthenticated = 65,

    /// <summary>fiskaly SCU transitioned to INITIALIZED (FON registration).</summary>
    FiskalyScuInitialized = 66,

    /// <summary>fiskaly cash register transitioned to INITIALIZED (initial receipt).</summary>
    FiskalyCashRegisterInitialized = 67,

    /// <summary>Development-only Super Admin fiskaly SIGN AT test receipt signed (not a POS payment).</summary>
    FiskalyTestReceiptSigned = 68,

    /// <summary>Unified REGK key activated (deployment or mandant).</summary>
    LicenseActivated = 69,

    /// <summary>Unified REGK key revoked / deactivated.</summary>
    LicenseRevoked = 70,

    /// <summary>Unified REGK activation rejected (format, not found, already used, slug mismatch).</summary>
    LicenseActivationFailed = 71,

    /// <summary>Reserved: Super Admin previewed a key (not written on every FA keystroke).</summary>
    LicensePreviewed = 72,

    /// <summary>Super Admin changed the FinanzOnline outbox worker enable/disable overlay.</summary>
    FinanzOnlineOutboxSettingsChanged = 73,

    /// <summary>Super Admin force-logged-out a user (security stamp + all sessions).</summary>
    UserForceLogout = 74,

    /// <summary>Super Admin terminated one or more auth sessions (refresh tokens revoked).</summary>
    UserSessionTerminated = 75,

    /// <summary>Super Admin changed the instance-wide RKSV Demo/Production overlay.</summary>
    RksvRuntimeConfigChanged = 76,

    /// <summary>POS/FA storno signed at fiskaly SIGN AT as receipt_type=CANCELLATION.</summary>
    FiskalyCancellationReceiptSigned = 77,

    /// <summary>SuperAdmin Fiskaly/RKSV receipt operation failed (structured error envelope).</summary>
    FiskalyReceiptOperationFailed = 78,

    /// <summary>SuperAdmin Fiskaly/RKSV receipt operation succeeded (normal, storno, or Sonderbeleg).</summary>
    FiskalyReceiptSigned = 79,

    /// <summary>Failed Fiskaly operation retried from FA history.</summary>
    FiskalyOperationRetried = 80,

    /// <summary>FA batch Fiskaly storno, Sonderbelege, or SuperAdmin DEP export finished (summary).</summary>
    FiskalyBatchCompleted = 81,

    /// <summary>Failed Fiskaly history row marked resolved / known issue / reopened.</summary>
    FiskalyErrorReviewUpdated = 82,

    /// <summary>Super Admin changed backup Hot/Warm/Cold legal retention policy.</summary>
    BackupRetentionPolicyUpdated = 83,

    /// <summary>Backup artifacts moved to Cold / WORM archive.</summary>
    BackupMovedToColdStorage = 84,

    /// <summary>Legal hold set or cleared on a backup run.</summary>
    BackupLegalHoldChanged = 85,

    /// <summary>Backup run enqueued (manual, scheduled cron, or operator API).</summary>
    BackupCreated = 86,

    /// <summary>Backup artifact downloaded to an operator workstation.</summary>
    BackupDownloaded = 87,

    /// <summary>Backup checksum / content verification completed.</summary>
    BackupVerified = 88,

    /// <summary>Cashier requested Mandanten-Admin to open a closed cash register.</summary>
    CashRegisterOpenRequested = 89,

    /// <summary>Mandanten-Admin approved a POS cash register open request.</summary>
    CashRegisterOpenRequestApproved = 90,

    /// <summary>Mandanten-Admin denied a POS cash register open request.</summary>
    CashRegisterOpenRequestDenied = 91,

    /// <summary>Cashier notified Mandanten-Admin that Monatsbeleg is missing.</summary>
    MonatsbelegManagerContacted = 92,

    /// <summary>Mandanten-Admin changed Monatsbeleg sales-blocking policy.</summary>
    MonatsbelegPolicyChanged = 93,

    /// <summary>TSE-signed Monatsbeleg created (including automatic system actor).</summary>
    MonatsbelegCreated = 94,

    /// <summary>Automatic Monatsbeleg creation exhausted retries.</summary>
    MonatsbelegAutoCreateFailed = 95,

    /// <summary>Super Admin created a mandant with an explicit country + VAT regime.</summary>
    TenantCreatedWithCountry = 96,

    /// <summary>Super Admin changed a mandant's operating country and/or VAT regime.</summary>
    TenantCountryChanged = 97,

    /// <summary>
    /// Country change left historical invoices/receipts/payments stamped with their original
    /// <c>CountryCodeAtIssue</c> / <c>VatRegimeAtIssue</c> (row count in audit newValues).
    /// </summary>
    TenantCountryChangedHistoricalPreserved = 98,

    /// <summary>
    /// Auto-Monatsbeleg catch-up window closed with previous-month receipt still missing.
    /// Numeric 105: 99 is reserved for <see cref="Other"/>.
    /// </summary>
    MonatsbelegAutoCreateMissed = 105,

    /// <summary>CH canary built an MWST QR payload. No TSE signature. IBAN is not stored.</summary>
    ChMwstQrBuilt = 106,

    /// <summary>Super Admin set Fiscal.MwstCh=false for the CH canary tenant.</summary>
    ChMwstCanaryRolledBack = 107,

    /// <summary>EN 16931 Schematron passed. Peppol was not sent when the Access Point is unset.</summary>
    EinvoiceValidated = 108,

    /// <summary>Hosted or mock Peppol Access Point accepted the UBL invoice.</summary>
    EinvoiceSubmitted = 109,

    /// <summary>Schematron or Access Point rejected the invoice. Detail is rule ids or HTTP status, not the XML.</summary>
    EinvoiceSubmissionFailed = 110,

    Other = 99
}
