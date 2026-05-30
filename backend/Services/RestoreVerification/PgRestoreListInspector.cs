using System.Diagnostics;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class PgRestoreListInspector : IPgRestoreListInspector
{
    private readonly IOptionsMonitor<RestoreVerificationOptions> _options;
    private readonly ILogger<PgRestoreListInspector> _logger;

    public PgRestoreListInspector(
        IOptionsMonitor<RestoreVerificationOptions> options,
        ILogger<PgRestoreListInspector> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<PgRestoreListInspectResult> InspectDumpFileAsync(
        string absoluteDumpPath,
        CancellationToken cancellationToken = default)
    {
        var run = await RunPgRestoreListAsync(absoluteDumpPath, cancellationToken);
        if (run.MissingFile)
        {
            return new PgRestoreListInspectResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Dump file missing."
            };
        }

        if (run.CouldNotStartProcess)
        {
            return new PgRestoreListInspectResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Failed to start pg_restore."
            };
        }

        var lines = run.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

        if (!run.Success)
            _logger.LogWarning(
                "pg_restore --list failed: exitCode={ExitCode}, stderrLen={Len}",
                run.ExitCode,
                run.StdErr.Length);

        return new PgRestoreListInspectResult
        {
            Success = run.Success,
            ExitCode = run.ExitCode,
            NonEmptyLineCount = lines,
            StdErrSnippet = Truncate(run.StdErr.Trim(), 2000)
        };
    }

    public async Task<PgRestoreListTableCatalogResult> ReadTableDataCatalogAsync(
        string absoluteDumpPath,
        CancellationToken cancellationToken = default)
    {
        var run = await RunPgRestoreListAsync(absoluteDumpPath, cancellationToken);
        if (run.MissingFile)
        {
            return new PgRestoreListTableCatalogResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Dump file missing."
            };
        }

        if (run.CouldNotStartProcess)
        {
            return new PgRestoreListTableCatalogResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Failed to start pg_restore."
            };
        }

        if (!run.Success)
        {
            _logger.LogWarning(
                "pg_restore --list catalog read failed: exitCode={ExitCode}",
                run.ExitCode);
            return new PgRestoreListTableCatalogResult
            {
                Success = false,
                ExitCode = run.ExitCode,
                StdErrSnippet = Truncate(run.StdErr.Trim(), 2000)
            };
        }

        var entries = PgRestoreListTableDataParser.ParseTableDataEntries(run.StdOut);
        return new PgRestoreListTableCatalogResult
        {
            Success = true,
            ExitCode = run.ExitCode,
            TableDataEntries = entries
        };
    }

    private async Task<PgRestoreListProcessRun> RunPgRestoreListAsync(
        string absoluteDumpPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(absoluteDumpPath) || !File.Exists(absoluteDumpPath))
            return PgRestoreListProcessRun.Missing();

        var o = _options.CurrentValue;
        var exe = string.IsNullOrWhiteSpace(o.PgRestoreExecutablePath)
            ? "pg_restore"
            : o.PgRestoreExecutablePath.Trim();
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--list {QuoteArg(absoluteDumpPath)}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();
        var stdout = new StringBuilder();
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };

        try
        {
            if (!proc.Start())
                return PgRestoreListProcessRun.FailedToStart();

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(cancellationToken);

            return new PgRestoreListProcessRun(
                proc.ExitCode == 0,
                proc.ExitCode,
                stdout.ToString(),
                stderr.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "pg_restore --list process error");
            return new PgRestoreListProcessRun(false, -1, string.Empty, ex.Message);
        }
    }

    private sealed record PgRestoreListProcessRun(
        bool Success,
        int ExitCode,
        string StdOut,
        string StdErr)
    {
        public bool MissingFile { get; init; }
        public bool CouldNotStartProcess { get; init; }

        public static PgRestoreListProcessRun Missing() =>
            new(false, -1, string.Empty, string.Empty) { MissingFile = true };

        public static PgRestoreListProcessRun FailedToStart() =>
            new(false, -1, string.Empty, string.Empty) { CouldNotStartProcess = true };
    }

    private static string QuoteArg(string path)
    {
        if (path.Contains('"', StringComparison.Ordinal))
            throw new ArgumentException("Path contains double-quote.", nameof(path));
        return path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..max] + "…";
    }
}
