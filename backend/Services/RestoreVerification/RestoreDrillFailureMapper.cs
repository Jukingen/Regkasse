using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// <see cref="RestoreVerificationRun.FailureCode"/> değerlerinden makine sınıfı üretir (geriye dönük: bilinmeyen kodlar <see cref="RestoreDrillFailureCategory.None"/>).
/// </summary>
public static class RestoreDrillFailureMapper
{
    public static RestoreDrillFailureCategory CategoryFromFailureCode(string? failureCode) =>
        failureCode switch
        {
            null or "" => RestoreDrillFailureCategory.None,
            "NO_DUMP_AVAILABLE" => RestoreDrillFailureCategory.ArtifactResolution,
            "NO_ELIGIBLE_BACKUP_RUN" => RestoreDrillFailureCategory.ArtifactResolution,
            "PG_RESTORE_LIST_FAILED" => RestoreDrillFailureCategory.PgRestoreList,
            "RESTORE_ATTEMPT_NOT_ALLOWED" => RestoreDrillFailureCategory.Configuration,
            "ISOLATED_RESTORE_NOT_CONFIGURED" => RestoreDrillFailureCategory.Configuration,
            "ISOLATED_PG_RESTORE_CANCELLED" => RestoreDrillFailureCategory.CancelledOrTimeout,
            "ISOLATED_PG_RESTORE_EXCEPTION" => RestoreDrillFailureCategory.IsolatedPgRestore,
            "ISOLATED_PG_RESTORE_FAILED" => RestoreDrillFailureCategory.IsolatedPgRestore,
            "POST_RESTORE_CONTINUITY_SQL_FAILED" => RestoreDrillFailureCategory.PostRestoreContinuitySql,
            "FISCAL_VALIDATION_NOT_EXECUTED" => RestoreDrillFailureCategory.FiscalSqlScript,
            "FISCAL_VALIDATION_FAILED" => RestoreDrillFailureCategory.FiscalSqlScript,
            "INTEGRITY_CHECKS_FAILED" => RestoreDrillFailureCategory.LiveOperationalIntegrity,
            "APPLICATION_SMOKE_PROBE_FAILED" => RestoreDrillFailureCategory.ApplicationSmokeProbe,
            "RESTORED_DB_APPLICATION_SMOKE_FAILED" => RestoreDrillFailureCategory.RestoredDatabaseApplicationSmoke,
            "APPLICATION_SMOKE_INVALID_BASE_URL" => RestoreDrillFailureCategory.Configuration,
            "UNHANDLED_EXCEPTION" => RestoreDrillFailureCategory.UnhandledException,
            _ => RestoreDrillFailureCategory.None
        };
}
