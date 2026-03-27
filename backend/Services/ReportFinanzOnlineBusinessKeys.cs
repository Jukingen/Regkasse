using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services;

/// <summary>
/// Explicit, versioned FinanzOnline outbox business keys per formal report type.
/// Keys are human-readable and stable for a given report artefact + submission attempt index.
/// </summary>
public static class ReportFinanzOnlineBusinessKeys
{
    /// <param name="submissionAttemptIndex">0 = first enqueue; incremented after terminal failure retries (new payload hash).</param>
    public static string Tagesbericht(
        Guid cashRegisterId,
        DateTime viennaBusinessDateUnspecified,
        Guid reportId,
        int submissionAttemptIndex)
    {
        var d = new DateTime(viennaBusinessDateUnspecified.Year, viennaBusinessDateUnspecified.Month, viennaBusinessDateUnspecified.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var anchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(d);
        var dateKey = PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(anchorUtc);
        return $"v1:Tagesbericht:{cashRegisterId:N}:{dateKey}:{reportId:N}:a{submissionAttemptIndex}";
    }

    public static string Monatsbericht(
        string scopeKind,
        string viennaYearMonth,
        string registerToken,
        Guid reportId,
        int submissionAttemptIndex)
    {
        var ym = viennaYearMonth.Replace("-", "", StringComparison.Ordinal);
        return $"v1:Monatsbericht:{scopeKind}:{ym}:{registerToken}:{reportId:N}:a{submissionAttemptIndex}";
    }

    public static string Jahresbericht(
        string scopeKind,
        int viennaYear,
        string registerToken,
        Guid reportId,
        int submissionAttemptIndex) =>
        $"v1:Jahresbericht:{scopeKind}:{viennaYear:D4}:{registerToken}:{reportId:N}:a{submissionAttemptIndex}";

    /// <summary>Reserved for future Periodenbericht frozen-run FinanzOnline-compatible enqueue.</summary>
    public static string PeriodenberichtRun(Guid runId, int submissionAttemptIndex) =>
        $"v1:PeriodenberichtRun:{runId:N}:a{submissionAttemptIndex}";
}
