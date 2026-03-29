using System.Diagnostics;
using System.Text;
using KasseAPI_Final.Configuration;
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
        if (string.IsNullOrWhiteSpace(absoluteDumpPath) || !File.Exists(absoluteDumpPath))
        {
            return new PgRestoreListInspectResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Dump file missing."
            };
        }

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
            {
                return new PgRestoreListInspectResult
                {
                    Success = false,
                    ExitCode = -1,
                    StdErrSnippet = "Failed to start pg_restore."
                };
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(cancellationToken);

            var outText = stdout.ToString();
            var lines = outText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

            var ok = proc.ExitCode == 0;
            if (!ok)
                _logger.LogWarning(
                    "pg_restore --list failed: exitCode={ExitCode}, stderrLen={Len}",
                    proc.ExitCode,
                    stderr.Length);

            return new PgRestoreListInspectResult
            {
                Success = ok,
                ExitCode = proc.ExitCode,
                NonEmptyLineCount = lines,
                StdErrSnippet = Truncate(stderr.ToString().Trim(), 2000)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "pg_restore --list process error");
            return new PgRestoreListInspectResult
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = ex.Message
            };
        }
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
