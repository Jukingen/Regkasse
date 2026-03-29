namespace KasseAPI_Final.Services.Backup.PgDump;

/// <summary>
/// Runs <c>pg_dump</c> in a child process (worker scope only). Isolated for tests and to keep process logic out of orchestrator/controller.
/// </summary>
public interface IPgDumpProcessRunner
{
    Task<PgDumpRunResult> RunAsync(PgDumpProcessSpec spec, CancellationToken cancellationToken = default);
}
