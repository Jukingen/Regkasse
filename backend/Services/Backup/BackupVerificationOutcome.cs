namespace KasseAPI_Final.Services.Backup;

public sealed record BackupVerificationOutcome(
    bool Passed,
    bool CompletenessFlag,
    string? FailureReason,
    string DetailsJson);
