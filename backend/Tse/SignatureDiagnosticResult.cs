namespace KasseAPI_Final.Tse;

/// <summary>
/// RKSV Checklist 1–5 diagnostik adım sonucu.
/// </summary>
public record SignatureDiagnosticStep(
    int StepId,
    string Name,
    string Status,  // "PASS" | "FAIL"
    string? Evidence = null
);
