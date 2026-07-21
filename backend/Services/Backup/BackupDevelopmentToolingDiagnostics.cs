using System.Diagnostics;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Geliştirme ortamında pg_dump / pg_restore önkoşullarını başlangıçta loglar — operatör hatası değil, DX.
/// </summary>
public static class BackupDevelopmentToolingDiagnostics
{
    private static readonly TimeSpan ToolProbeTimeout = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Development + PgDump adaptöründe istemci araçlarının süreçten çalıştığını doğrular; sonuç API tanılarına yazılır.
    /// </summary>
    public static void LogPostgresClientToolingIfDevelopment(
        IHostEnvironment environment,
        ILogger logger,
        BackupOptions backupOptions,
        RestoreVerificationOptions restoreOptions,
        IBackupPostgresClientToolingProbeState? probeState = null)
    {
        var snap = ProbePostgresClientTools(environment, backupOptions, restoreOptions, logger);
        probeState?.SetSnapshot(snap);
    }

    /// <summary>
    /// pg_dump / pg_restore için --version probu; Development dışında veya PgDump seçili değilse <see cref="BackupPostgresClientToolingSnapshot.SkippedNotApplicable"/>.
    /// </summary>
    public static BackupPostgresClientToolingSnapshot ProbePostgresClientTools(
        IHostEnvironment environment,
        BackupOptions backupOptions,
        RestoreVerificationOptions restoreOptions,
        ILogger? logger)
    {
        if (!environment.IsDevelopment())
            return BackupPostgresClientToolingSnapshot.SkippedNotApplicable;

        if (backupOptions.ExecutionAdapterKind != BackupExecutionAdapterKind.PgDump)
        {
            logger?.LogInformation(
                "Backup development tooling: ExecutionAdapterKind={Adapter} — pg_dump/pg_restore probes skipped (not used for this adapter).",
                backupOptions.ExecutionAdapterKind);
            return new BackupPostgresClientToolingSnapshot
            {
                ProbesSkipped = true,
                SkipReason = "adapter_not_pg_dump"
            };
        }

        var pgDumpExe = string.IsNullOrWhiteSpace(backupOptions.PgDumpExecutablePath)
            ? "pg_dump"
            : backupOptions.PgDumpExecutablePath.Trim();
        var pgRestoreExe = string.IsNullOrWhiteSpace(restoreOptions.PgRestoreExecutablePath)
            ? "pg_restore"
            : restoreOptions.PgRestoreExecutablePath.Trim();

        logger?.LogInformation(
            "Backup development tooling: PgDump selected — effective pg_dump executable token={PgDumpToken}, pg_restore token={PgRestoreToken} (set Backup:PgDumpExecutablePath / RestoreVerification:PgRestoreExecutablePath if not on PATH).",
            pgDumpExe,
            pgRestoreExe);

        var dump = ProbeTool(logger, "pg_dump", pgDumpExe, "Backup:PgDumpExecutablePath");
        var restore = ProbeTool(logger, "pg_restore", pgRestoreExe, "RestoreVerification:PgRestoreExecutablePath");

        return new BackupPostgresClientToolingSnapshot
        {
            ProbesSkipped = false,
            PgDumpExecutableToken = pgDumpExe,
            PgDumpProbeSucceeded = dump.Success,
            PgDumpFailureKind = dump.FailureKind,
            PgDumpVersionLine = dump.VersionLine,
            PgRestoreExecutableToken = pgRestoreExe,
            PgRestoreProbeSucceeded = restore.Success,
            PgRestoreFailureKind = restore.FailureKind,
            PgRestoreVersionLine = restore.VersionLine
        };
    }

    private static (bool Success, string? FailureKind, string? VersionLine) ProbeTool(
        ILogger? logger,
        string toolName,
        string executableToken,
        string configKeyHint)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executableToken,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!p.Start())
            {
                logger?.LogWarning(
                    "Backup development tooling: could not start {Tool} ({Executable}) — check PATH or {ConfigKey}.",
                    toolName,
                    executableToken,
                    configKeyHint);
                return (false, "START_FAILED", null);
            }

            if (!p.WaitForExit((int)ToolProbeTimeout.TotalMilliseconds))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                logger?.LogWarning(
                    "Backup development tooling: {Tool} ({Executable}) timed out after {TimeoutSeconds}s — verify install or {ConfigKey}.",
                    toolName,
                    executableToken,
                    (int)ToolProbeTimeout.TotalSeconds,
                    configKeyHint);
                return (false, "TIMEOUT", null);
            }

            var stdout = p.StandardOutput.ReadToEnd();
            var firstLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                ?? string.Empty;
            if (p.ExitCode == 0 && firstLine.Length > 0)
            {
                var line = firstLine.Length > 200 ? firstLine[..200] + "…" : firstLine;
                logger?.LogInformation(
                    "Backup development tooling: {Tool} OK — {VersionLine}",
                    toolName,
                    line);
                return (true, null, line);
            }

            var stderr = p.StandardError.ReadToEnd();
            logger?.LogWarning(
                "Backup development tooling: {Tool} ({Executable}) exitCode={ExitCode}. stderr: {StdErr}. Install PostgreSQL client tools or set {ConfigKey}.",
                toolName,
                executableToken,
                p.ExitCode,
                stderr.Length > 500 ? stderr[..500] + "…" : stderr,
                configKeyHint);
            return (false, p.ExitCode != 0 ? "NONZERO_EXIT" : "MISSING_OUTPUT", null);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Backup development tooling: failed to probe {Tool} ({Executable}) — install PostgreSQL client tools or set {ConfigKey}.",
                toolName,
                executableToken,
                configKeyHint);
            return (false, "EXCEPTION", null);
        }
    }
}
