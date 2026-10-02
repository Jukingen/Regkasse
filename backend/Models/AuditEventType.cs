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

    /// <summary>DE canary: TSS or client id stored on company settings for the first time. No API secrets.</summary>
    KsDeTssCreated = 100,

    /// <summary>DE SIGN start. Declared here; fired from the fiscal signature router.</summary>
    KsDeTxStarted = 101,

    /// <summary>DE SIGN finish. Declared here; fired from the fiscal signature router.</summary>
    KsDeTxFinished = 102,

    /// <summary>DSFinV-K export requested. This package records PENDING only.</summary>
    KsDeExportCreated = 103,

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

    /// <summary>CH QR-Rechnung SPC payload built. Stores a hash only. The payload contains an IBAN and is not logged.</summary>
    QrRechnungPayloadBuilt = 111,

    /// <summary>CH QR-Rechnung PDF rendered. Stores a relative path when the caller supplied one. No absolute path and no IBAN.</summary>
    QrRechnungPdfGenerated = 112,

    /// <summary>Super Admin registered a Peppol participant id. No credential is stored.</summary>
    PeppolParticipantRegistered = 113,

    /// <summary>
    /// Operator acknowledged open CH QR print gaps for one tenant.
    /// This is not a SIX or MWST compliance claim and it does not enable bank submission.
    /// </summary>
    ChQrKnownGapsAccepted = 114,

    /// <summary>
    /// Storecove reported the canary document as delivered. Not a Peppol compliance claim.
    /// The UBL document is not stored in the audit row.
    /// </summary>
    EinvoiceAckReceived = 115,

    /// <summary>
    /// Super Admin downloaded the QR-Rechnung PDF. No bank HTTP. The IBAN is not stored.
    /// </summary>
    QrRechnungPdfDownloaded = 116,

    /// <summary>
    /// Operator recorded that the PDF was uploaded in a bank portal. No bank HTTP.
    /// </summary>
    QrRechnungBankUploadConfirmed = 117,

    /// <summary>
    /// A transient Storecove ACK read was scheduled again. Not a Peppol compliance claim.
    /// The UBL document and the API key are not stored.
    /// </summary>
    EinvoiceSubmissionRetry = 118,

    /// <summary>Super Admin changed a tenant's POS vertical profile and/or its JSON overrides.</summary>
    TenantVerticalProfileChanged = 119,

    /// <summary>POS created a tenant-scoped appointment (not a fiscal receipt).</summary>
    AppointmentCreated = 120,

    /// <summary>POS updated a tenant-scoped appointment with a version check.</summary>
    AppointmentUpdated = 121,

    /// <summary>POS soft-cancelled a tenant-scoped appointment.</summary>
    AppointmentCancelled = 122,

    /// <summary>
    /// A sale stored a non-fiscal prescription reference. The reference itself is not logged.
    /// Not part of the RKSV/TSE chain.
    /// </summary>
    PaymentWithPrescription = 123,

    /// <summary>A ticket-sales sale issued a redeemable ticket. The plaintext code is not logged.</summary>
    TicketIssued = 124,

    /// <summary>POS redeemed a ticket. The plaintext code is not logged.</summary>
    TicketRedeemed = 125,

    /// <summary>POS sent a non-fiscal kitchen order from the current cart.</summary>
    KitchenOrderCreated = 126,

    /// <summary>Kitchen or POS changed a kitchen order or item status.</summary>
    KitchenOrderStatusChanged = 127,

    /// <summary>POS cancelled a kitchen order that was not served.</summary>
    KitchenOrderCancelled = 128,

    /// <summary>A lodging room was created. Not a fiscal event.</summary>
    RoomCreated = 129,

    /// <summary>A guest folio was opened for a room. Not a fiscal receipt.</summary>
    GuestFolioOpened = 130,

    /// <summary>Super Admin created a POS vertical profile (blank or cloned). Not a fiscal event.</summary>
    VerticalProfileCreated = 131,

    /// <summary>Super Admin edited a POS vertical profile. Not a fiscal event.</summary>
    VerticalProfileUpdated = 132,

    /// <summary>Super Admin soft-deleted a custom POS vertical profile. Not a fiscal event.</summary>
    VerticalProfileDeleted = 133,

    /// <summary>A lodging room status changed. Not a fiscal event.</summary>
    RoomStatusChanged = 134,

    /// <summary>A guest folio was closed or cancelled. Not a fiscal receipt.</summary>
    FolioClosed = 135,

    /// <summary>A non-fiscal charge was added to an open guest folio. No TSE receipt.</summary>
    FolioCharged = 136,

    Other = 99
}
