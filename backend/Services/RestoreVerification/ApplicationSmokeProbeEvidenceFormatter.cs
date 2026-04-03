namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>L5b: HTTP duman kanıtı için kısa özet (salt okunur GET).</summary>
public static class ApplicationSmokeProbeEvidenceFormatter
{
    public static string MapApplicationSmokeResult(bool success) => success ? "passed" : "failed";

    public static string BuildApplicationSmokeSummary(bool success, int? httpStatus, long? durationMs, string? path)
    {
        var r = MapApplicationSmokeResult(success);
        var st = httpStatus?.ToString() ?? "n/a";
        var ms = durationMs?.ToString() ?? "n/a";
        var p = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        return $"L5b HTTP application smoke: result={r}, http_status={st}, duration_ms={ms}, path={p}";
    }
}
