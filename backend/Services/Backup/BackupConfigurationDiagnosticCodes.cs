namespace KasseAPI_Final.Services.Backup;

/// <summary>Stable codes for <see cref="BackupConfigurationDiagnostic.Code"/> — safe for UI i18n lookup.</summary>
public static class BackupConfigurationDiagnosticCodes
{
    public const string DevAdapterFakeNoPgDump = "BACKUP_SETUP_DEV_ADAPTER_FAKE_NO_REAL_PG_DUMP";

    public const string DevAdapterProductionStubNoPgDump = "BACKUP_SETUP_DEV_ADAPTER_PRODUCTION_STUB_NO_REAL_PG_DUMP";

    public const string FakeAdapterForbiddenProduction = "BACKUP_SETUP_FAKE_ADAPTER_FORBIDDEN_NON_DEV";

    public const string FakeAdapterAcknowledgedProduction = "BACKUP_SETUP_FAKE_ADAPTER_ACKNOWLEDGED_NON_DEV";

    public const string ProductionStubForbiddenProduction = "BACKUP_SETUP_PRODUCTION_STUB_FORBIDDEN_NON_DEV";

    public const string ProductionStubAcknowledgedProduction = "BACKUP_SETUP_PRODUCTION_STUB_ACKNOWLEDGED_NON_DEV";

    public const string PgDumpStagingRootMissing = "BACKUP_SETUP_PG_DUMP_STAGING_ROOT_MISSING";

    public const string PgDumpStagingRootNotAbsolute = "BACKUP_SETUP_PG_DUMP_STAGING_ROOT_NOT_ABSOLUTE_NON_DEV";

    public const string PgDumpVerifyOnDiskRequired = "BACKUP_SETUP_PG_DUMP_VERIFY_ON_DISK_REQUIRED_NON_DEV";

    public const string PgDumpExternalArchiveRequired = "BACKUP_SETUP_PG_DUMP_EXTERNAL_ARCHIVE_REQUIRED_NON_DEV";

    public const string DevExternalArchiveNotSet = "BACKUP_SETUP_DEV_EXTERNAL_ARCHIVE_NOT_CONFIGURED";

    public const string ExternalArchiveRootNotAbsolute = "BACKUP_SETUP_EXTERNAL_ARCHIVE_ROOT_NOT_ABSOLUTE";

    public const string ExternalArchiveImmutabilityMismatch = "BACKUP_SETUP_EXTERNAL_ARCHIVE_IMMUTABILITY_ATTESTATION_MISMATCH";

    public const string ExternalArchiveOperatorDispositionMissing = "BACKUP_SETUP_EXTERNAL_ARCHIVE_OPERATOR_DISPOSITION_MISSING_NON_DEV";

    public const string ExternalArchiveImmutableAttestationOnly = "BACKUP_SETUP_EXTERNAL_ARCHIVE_IMMUTABLE_ATTESTATION_ONLY";

    public const string PgDumpConnectionStringMissing = "BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_MISSING";

    public const string PgDumpConnectionStringIncomplete = "BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_INCOMPLETE";

    public const string PgDumpConnectionStringInvalid = "BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_INVALID";

    public const string OrchestratorPollingTooShort = "BACKUP_SETUP_ORCHESTRATOR_POLLING_INTERVAL_TOO_SHORT";

    public const string OrchestratorPollingTooLong = "BACKUP_SETUP_ORCHESTRATOR_POLLING_INTERVAL_TOO_LONG";

    public const string WorkerDisabled = "BACKUP_SETUP_WORKER_DISABLED";

    public const string OrchestratorLockDisabledNonDev = "BACKUP_SETUP_ORCHESTRATOR_DISTRIBUTED_LOCK_DISABLED_NON_DEV";

    public const string DevelopmentForceVerificationFailureNonDev = "BACKUP_SETUP_DEV_FORCE_VERIFICATION_FAILURE_NON_DEV";

    public const string RetentionExecutionPlanned = "BACKUP_SETUP_RETENTION_EXECUTION_PLANNED_NOT_IMPLEMENTED";

    public const string ScheduledBackupCronMissing = "BACKUP_SETUP_SCHEDULED_BACKUP_CRON_MISSING";

    public const string ScheduledBackupCronInvalid = "BACKUP_SETUP_SCHEDULED_BACKUP_CRON_INVALID";

    public const string DevPgDumpClientMissingOrBroken = "BACKUP_SETUP_DEV_PG_DUMP_CLIENT_MISSING_OR_BROKEN";

    public const string DevPgRestoreClientMissingOrBroken = "BACKUP_SETUP_DEV_PG_RESTORE_CLIENT_MISSING_OR_BROKEN";

    public const string DevPgDumpClientOk = "BACKUP_SETUP_DEV_PG_DUMP_CLIENT_OK";

    public const string DevPgRestoreClientOk = "BACKUP_SETUP_DEV_PG_RESTORE_CLIENT_OK";

    public const string RealDumpAdapterSelectedConfigHealthy = "BACKUP_SETUP_REAL_DUMP_ADAPTER_SELECTED_CONFIG_HEALTHY";
}
