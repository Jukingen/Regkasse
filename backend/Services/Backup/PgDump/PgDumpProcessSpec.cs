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

    /// <summary><c>pg_dump -Z</c> level 0–9 for custom-format zlib compression.</summary>
    public int CompressionLevel { get; init; } = 6;

    /// <summary>Optional tables to pass as <c>--exclude-table</c> (credentials / cache).</summary>
    public IReadOnlyList<string> ExcludeTables { get; init; } = Array.Empty<string>();
}
