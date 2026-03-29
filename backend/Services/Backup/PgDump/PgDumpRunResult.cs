namespace KasseAPI_Final.Services.Backup.PgDump;

public sealed class PgDumpRunResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string StdErr { get; init; } = string.Empty;
    public string StdOut { get; init; } = string.Empty;
}
