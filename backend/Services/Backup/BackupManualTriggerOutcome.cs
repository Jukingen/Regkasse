using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupManualTriggerOutcome
{
    public required BackupRun Run { get; init; }

    public BackupManualTriggerResultKind Kind { get; init; }

    public bool DuplicateExecutionPrevented => Kind == BackupManualTriggerResultKind.DuplicateActiveManualPrevented;
}
