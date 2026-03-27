using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

/// <summary>
/// Kurze deutsche Operatör-Hinweise für Upstream-Propagations-Codes (API: NoteDe).
/// </summary>
public static class FormalReportPropagationNotes
{
    public static string? NoteDe(string? reasonCode) => reasonCode switch
    {
        FormalReportPropagationMarkers.ReasonTagesSupersededInMonth =>
            "Ein Tagesbericht in diesem Zeitraum wurde korrigiert. Monats- und Jahresansichten können vom eingefrorenen Snapshot abweichen — bitte prüfen oder neuen Entwurf erzeugen.",
        FormalReportPropagationMarkers.ReasonMonatsSupersededInYear =>
            "Ein Monatsbericht in diesem Jahr wurde korrigiert. Der Jahresbericht kann vom eingefrorenen Snapshot abweichen — bitte prüfen oder neuen Entwurf erzeugen.",
        _ => null
    };

    public static FormalReportUpstreamPropagationDto ToUpstreamPropagationDto(MonatsberichtReport row) => new()
    {
        RequiresReview = row.UpstreamReviewRequired,
        ReasonCode = row.UpstreamReviewReasonCode,
        NoteDe = NoteDe(row.UpstreamReviewReasonCode)
    };

    public static FormalReportUpstreamPropagationDto ToUpstreamPropagationDto(JahresberichtReport row) => new()
    {
        RequiresReview = row.UpstreamReviewRequired,
        ReasonCode = row.UpstreamReviewReasonCode,
        NoteDe = NoteDe(row.UpstreamReviewReasonCode)
    };
}
