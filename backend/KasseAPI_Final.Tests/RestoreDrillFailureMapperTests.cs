using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreDrillFailureMapperTests
{
    [Theory]
    [InlineData("POST_RESTORE_CONTINUITY_SQL_FAILED", RestoreDrillFailureCategory.PostRestoreContinuitySql)]
    [InlineData("NO_ELIGIBLE_BACKUP_RUN", RestoreDrillFailureCategory.ArtifactResolution)]
    [InlineData("NO_DUMP_AVAILABLE", RestoreDrillFailureCategory.ArtifactResolution)]
    [InlineData("PG_RESTORE_LIST_FAILED", RestoreDrillFailureCategory.PgRestoreList)]
    [InlineData("RESTORE_ATTEMPT_NOT_ALLOWED", RestoreDrillFailureCategory.Configuration)]
    [InlineData("ISOLATED_RESTORE_NOT_CONFIGURED", RestoreDrillFailureCategory.Configuration)]
    [InlineData("ISOLATED_PG_RESTORE_CANCELLED", RestoreDrillFailureCategory.CancelledOrTimeout)]
    [InlineData("ISOLATED_PG_RESTORE_EXCEPTION", RestoreDrillFailureCategory.IsolatedPgRestore)]
    [InlineData("ISOLATED_PG_RESTORE_FAILED", RestoreDrillFailureCategory.IsolatedPgRestore)]
    [InlineData("FISCAL_VALIDATION_NOT_EXECUTED", RestoreDrillFailureCategory.FiscalSqlScript)]
    [InlineData("FISCAL_VALIDATION_FAILED", RestoreDrillFailureCategory.FiscalSqlScript)]
    [InlineData("INTEGRITY_CHECKS_FAILED", RestoreDrillFailureCategory.LiveOperationalIntegrity)]
    [InlineData("APPLICATION_SMOKE_PROBE_FAILED", RestoreDrillFailureCategory.ApplicationSmokeProbe)]
    [InlineData("RESTORED_DB_APPLICATION_SMOKE_FAILED", RestoreDrillFailureCategory.RestoredDatabaseApplicationSmoke)]
    [InlineData("UNHANDLED_EXCEPTION", RestoreDrillFailureCategory.UnhandledException)]
    [InlineData("UNKNOWN_FUTURE_CODE", RestoreDrillFailureCategory.None)]
    [InlineData(null, RestoreDrillFailureCategory.None)]
    public void CategoryFromFailureCode_maps_expected_strings(string? code, RestoreDrillFailureCategory expected) =>
        Assert.Equal(expected, RestoreDrillFailureMapper.CategoryFromFailureCode(code));
}
