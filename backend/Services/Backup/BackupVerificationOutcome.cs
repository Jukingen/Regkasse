namespace KasseAPI_Final.Services.Backup;

/// <param name="Passed">Metadata / isteğe bağlı disk SHA doğrulaması geçti.</param>
/// <param name="CompletenessFlag">Doğrulanan listede <see cref="KasseAPI_Final.Models.Backup.BackupArtifactType.LogicalDump"/> var.</param>
/// <param name="FailureReason">Geçişsiz ise hata metni.</param>
/// <param name="DetailsJson">Operatör / log için JSON (restore kanıtı değil).</param>
public sealed record BackupVerificationOutcome(
    bool Passed,
    bool CompletenessFlag,
    string? FailureReason,
    string DetailsJson);
