namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class PgRestoreListInspectResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public int NonEmptyLineCount { get; init; }
    public string? StdErrSnippet { get; init; }
}
