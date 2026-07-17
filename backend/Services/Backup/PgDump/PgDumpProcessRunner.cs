using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup.PgDump;

/// <summary>
/// Production implementation: spawns pg_dump with connection env vars. Does not use shell / cmd.exe.
/// </summary>
public sealed class PgDumpProcessRunner : IPgDumpProcessRunner
{
    private readonly ILogger<PgDumpProcessRunner> _logger;

    public PgDumpProcessRunner(ILogger<PgDumpProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<PgDumpRunResult> RunAsync(PgDumpProcessSpec spec, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spec.ExecutablePath))
            throw new ArgumentException("ExecutablePath is required.", nameof(spec));

        var wall = Stopwatch.StartNew();

        var args = new StringBuilder();
        args.Append("-h ").Append(QuoteArg(spec.Host));
        args.Append(" -p ").Append(spec.Port);
        args.Append(" -U ").Append(QuoteArg(spec.User));
        args.Append(" -Fc --no-owner --no-acl ");
        var z = Math.Clamp(spec.CompressionLevel, 0, 9);
        args.Append("-Z").Append(z).Append(' ');
        foreach (var table in spec.ExcludeTables)
        {
            if (string.IsNullOrWhiteSpace(table))
                continue;
            args.Append("--exclude-table=").Append(QuoteArg(table.Trim())).Append(' ');
        }
        args.Append("-f ").Append(QuoteArg(spec.OutputFilePath));
        args.Append(' ').Append(QuoteArg(spec.Database));

        var psi = new ProcessStartInfo
        {
            FileName = spec.ExecutablePath,
            Arguments = args.ToString(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Avoid passing password on command line (visible in ps).
        psi.Environment["PGPASSWORD"] = spec.Password;

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();
        var stdout = new StringBuilder();
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };

        _logger.LogInformation(
            "Starting pg_dump: database={Database}, host={Host}, outputFile={OutputFile}",
            spec.Database,
            spec.Host,
            spec.OutputFilePath);

        try
        {
            if (!proc.Start())
            {
                return new PgDumpRunResult
                {
                    Success = false,
                    ExitCode = -1,
                    StdErr = "Failed to start pg_dump process."
                };
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(spec.Timeout);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "pg_dump kill after timeout failed");
                }

                return new PgDumpRunResult
                {
                    Success = false,
                    ExitCode = -2,
                    StdErr = $"pg_dump timed out after {spec.Timeout}."
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "pg_dump kill after cancellation failed");
                }

                return new PgDumpRunResult
                {
                    Success = false,
                    ExitCode = -3,
                    StdErr = "pg_dump cancelled (shutdown or cooperative cancellation token)."
                };
            }

            var err = stderr.ToString().Trim();
            var @out = stdout.ToString().Trim();
            var ok = proc.ExitCode == 0;
            wall.Stop();
            if (!ok)
                _logger.LogWarning(
                    "pg_dump exited with code {ExitCode}. stderr length={StdErrLen}, stdout length={StdOutLen}, elapsedMs={ElapsedMs}",
                    proc.ExitCode,
                    err.Length,
                    @out.Length,
                    wall.ElapsedMilliseconds);
            else
                _logger.LogInformation(
                    "pg_dump finished: exitCode={ExitCode}, elapsedMs={ElapsedMs}, database={Database}, host={Host}",
                    proc.ExitCode,
                    wall.ElapsedMilliseconds,
                    spec.Database,
                    spec.Host);

            return new PgDumpRunResult
            {
                Success = ok,
                ExitCode = proc.ExitCode,
                StdErr = err,
                StdOut = @out
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "pg_dump process failure");
            return new PgDumpRunResult
            {
                Success = false,
                ExitCode = -1,
                StdErr = ex.Message
            };
        }
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        if (value.Contains('"', StringComparison.Ordinal))
            throw new ArgumentException("Argument contains double-quote; refuse unsafe escaping.", nameof(value));

        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
    }
}
