using KasseAPI_Final.Services.Backup;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class PgRestoreListTableCatalogResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string? StdErrSnippet { get; init; }
    public IReadOnlyList<PgRestoreListTableEntry> TableDataEntries { get; init; } = Array.Empty<PgRestoreListTableEntry>();
}
