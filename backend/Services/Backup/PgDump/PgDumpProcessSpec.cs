namespace KasseAPI_Final.Services.Backup.PgDump;

/// <summary>
/// Inputs for one pg_dump invocation. Password is never logged by the runner.
/// </summary>
public sealed class PgDumpProcessSpec
{
    public required string ExecutablePath { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; } = 5432;
    public required string User { get; init; }
    public required string Password { get; init; }
    public required string Database { get; init; }
    public required string OutputFilePath { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(2);
}
