using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L5a: geri yüklenen DB dumanı için kısa makine metni (HTTP L5b ile karıştırılmaz).
/// </summary>
public static class RestoredDatabaseApplicationSmokeEvidenceFormatter
{
    public static string MapApplicationSmokeResult(RestoreDrillApplicationSmokeResultKind kind) =>
        kind switch
        {
            RestoreDrillApplicationSmokeResultKind.Passed => "passed",
            RestoreDrillApplicationSmokeResultKind.Failed => "failed",
            RestoreDrillApplicationSmokeResultKind.NotAttempted => "not_attempted",
            RestoreDrillApplicationSmokeResultKind.NotSupported => "not_supported",
            RestoreDrillApplicationSmokeResultKind.Inconclusive => "inconclusive",
            _ => "unknown"
        };

    public static string BuildApplicationSmokeSummary(
        RestoreDrillApplicationSmokeResultKind kind,
        string? detail,
        long? durationMs)
    {
        var r = MapApplicationSmokeResult(kind);
        var d = string.IsNullOrWhiteSpace(detail) ? "" : detail.Trim();
        var ms = durationMs?.ToString() ?? "n/a";
        return $"L5a restored DB smoke: result={r}, detail={d}, duration_ms={ms}";
    }
}
