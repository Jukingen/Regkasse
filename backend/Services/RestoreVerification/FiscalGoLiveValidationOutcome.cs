namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class FiscalGoLiveValidationOutcome
{
    public bool Executed { get; init; }
    public bool Passed { get; init; }
    public int FailCount { get; init; }
    public int WarnCount { get; init; }
    public string? SummaryLine { get; init; }
    public string? ErrorDetail { get; init; }
}
