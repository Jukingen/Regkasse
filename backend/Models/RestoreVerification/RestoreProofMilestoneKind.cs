using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Kanıt kilometre taşı türü; UI anahtarı (backend üretir).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RestoreProofMilestoneKind
{
    [JsonStringEnumMemberName("latestBackupRun")]
    LatestBackupRun,

    [JsonStringEnumMemberName("latestPgDumpSucceededBackupRun")]
    LatestPgDumpSucceededBackupRun,

    [JsonStringEnumMemberName("latestPgDumpSucceededArtifact")]
    LatestPgDumpSucceededArtifact,

    [JsonStringEnumMemberName("latestRestoreDrillAttempt")]
    LatestRestoreDrillAttempt,

    [JsonStringEnumMemberName("latestRestoreDrillSucceeded")]
    LatestRestoreDrillSucceeded,

    /// <summary>Son tamamlanan ve L4 süreklilik SQL kanıtı geçen drill.</summary>
    [JsonStringEnumMemberName("lastKnownGoodL4ContinuityProven")]
    LastKnownGoodL4ContinuityProven,

    [JsonStringEnumMemberName("lastKnownGoodL5HttpSmokeProven")]
    LastKnownGoodL5HttpSmokeProven,

    [JsonStringEnumMemberName("lastKnownGoodL5aRestoredDbSmokeProven")]
    LastKnownGoodL5aRestoredDbSmokeProven
}
