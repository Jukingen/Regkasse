using System.Reflection;
using System.Text.Json;
using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Stable JSON property surface for <c>/api/admin/backup/recoverability-summary</c> (camelCase API contract).
/// </summary>
public sealed class BackupRecoverabilitySummaryResponseDtoContractTests
{
    [Fact]
    public void RecoverabilitySummaryResponseDto_exposes_stable_camelCase_json_properties()
    {
        var expected = new[]
        {
            "backupExecutionReality",
            "backupProofAgeSeconds",
            "backupReadinessLevel",
            "backupReadinessNarrative",
            "lastSuccessfulArtifactVerificationAt",
            "lastSuccessfulBackupAt",
            "lastSuccessfulBackupRunId",
            "lastSuccessfulBackupRunIsSimulatedExecution",
            "lastSuccessfulRestoreProofAt",
            "lastSuccessfulRestoreProofRunId",
            "latestRestoreRunAt",
            "latestRestoreRunStatus",
            "latestRunAt",
            "latestRunStatus",
            "realPostgreSqlLogicalDumpConfigured",
            "restoreProofAgeSeconds"
        };

        var actual = typeof(BackupRecoverabilitySummaryResponseDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
