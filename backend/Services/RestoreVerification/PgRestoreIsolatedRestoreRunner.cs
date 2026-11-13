using System.Diagnostics;
using System.Text;
using KasseAPI_Final.Services.Backup;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Shell yok; <c>pg_restore</c> child process + Npgsql ile CREATE/DROP DATABASE.
/// </summary>
public sealed class PgRestoreIsolatedRestoreRunner : IPgRestoreIsolatedRestoreRunner
{
    private readonly IBackupEncryptionService _encryption;
    private readonly ILogger<PgRestoreIsolatedRestoreRunner> _logger;

    public PgRestoreIsolatedRestoreRunner(
        IBackupEncryptionService encryption,
        ILogger<PgRestoreIsolatedRestoreRunner> logger)
    {
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<PgRestoreIsolatedRestoreOutcome> RestoreCustomDumpToEphemeralDatabaseAsync(
        string adminConnectionString,
        string absoluteCustomDumpPath,
        string newDatabaseName,
        string? pgRestoreExecutablePath,
        TimeSpan timeout,
        bool dropEphemeralDatabaseAfterRestore = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminConnectionString))
            throw new ArgumentException("Admin connection string is required.", nameof(adminConnectionString));
        if (string.IsNullOrWhiteSpace(absoluteCustomDumpPath) || !File.Exists(absoluteCustomDumpPath))
        {
            return new PgRestoreIsolatedRestoreOutcome
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = "Dump file missing for isolated restore.",
                DatabaseName = newDatabaseName
            };
        }

        foreach (var c in newDatabaseName)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return new PgRestoreIsolatedRestoreOutcome
                {
                    Success = false,
                    ExitCode = -1,
                    StdErrSnippet = "Invalid database name for isolated restore (allowed: letters, digits, underscore).",
                    DatabaseName = newDatabaseName
                };
            }
        }

        NpgsqlConnectionStringBuilder adminB;
        try
        {
            adminB = new NpgsqlConnectionStringBuilder(adminConnectionString);
        }
        catch (Exception ex)
        {
            return new PgRestoreIsolatedRestoreOutcome
            {
                Success = false,
                ExitCode = -1,
                StdErrSnippet = $"Invalid admin connection string: {ex.Message}",
                DatabaseName = newDatabaseName
            };
        }

        var maintenanceDb = string.IsNullOrWhiteSpace(adminB.Database) ? "postgres" : adminB.Database;
        adminB.Database = maintenanceDb;
        var adminCs = adminB.ConnectionString;

        var databaseCreated = false;
        var restoreSucceeded = false;
        string? decryptedTempPath = null;
        try
        {
            var dumpPathForRestore = Path.GetFullPath(absoluteCustomDumpPath);
            var peek = new byte[BackupEncryptionService.HeaderSize];
            await using (var fs = File.OpenRead(dumpPathForRestore))
            {
                var read = await fs.ReadAsync(peek.AsMemory(0, peek.Length), cancellationToken);
                if (read >= BackupEncryptionService.Magic.Length
                    && _encryption.LooksEncrypted(peek.AsSpan(0, read)))
                {
                    decryptedTempPath = Path.Combine(
                        Path.GetTempPath(),
                        $"regkasse-restore-decrypt-{Guid.NewGuid():N}.dump");
                    await _encryption.DecryptFileToAsync(
                            dumpPathForRestore,
                            decryptedTempPath,
                            cancellationToken)
                        .ConfigureAwait(false);
                    dumpPathForRestore = decryptedTempPath;
                    _logger.LogInformation(
                        "Isolated pg_restore: decrypted encrypted dump to temp for restore drill.");
                }
            }

            await using (var conn = new NpgsqlConnection(adminCs))
            {
                await conn.OpenAsync(cancellationToken);
                await DropDatabaseIfExistsAsync(conn, newDatabaseName, cancellationToken);
                await using var createCmd = new NpgsqlCommand(
                    $"CREATE DATABASE \"{newDatabaseName}\"",
                    conn);
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
                databaseCreated = true;
            }

            var targetB = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = newDatabaseName
            };
            var password = targetB.Password ?? string.Empty;
            var exe = string.IsNullOrWhiteSpace(pgRestoreExecutablePath)
                ? "pg_restore"
                : pgRestoreExecutablePath.Trim();

            var args = new StringBuilder();
            args.Append("-h ").Append(QuoteArg(targetB.Host ?? "127.0.0.1"));
            args.Append(" -p ").Append(targetB.Port == 0 ? 5432 : targetB.Port);
            args.Append(" -U ").Append(QuoteArg(targetB.Username ?? ""));
            args.Append(" -d ").Append(QuoteArg(newDatabaseName));
            args.Append(" --no-owner --no-acl ");
            args.Append(QuoteArg(dumpPathForRestore));

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args.ToString(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["PGPASSWORD"] = password;

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };

            _logger.LogInformation(
                "Isolated pg_restore starting: database={Database}, dumpFile={DumpFile}",
                newDatabaseName,
                dumpPathForRestore);

            if (!proc.Start())
            {
                return Fail(newDatabaseName, -1, "Failed to start pg_restore process.");
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(proc);
                return Fail(newDatabaseName, -2, $"pg_restore timed out after {timeout}.");
            }
            catch (OperationCanceledException)
            {
                TryKill(proc);
                throw;
            }

            restoreSucceeded = proc.ExitCode == 0;
            if (!restoreSucceeded)
                _logger.LogWarning(
                    "Isolated pg_restore failed: database={Database}, exitCode={ExitCode}",
                    newDatabaseName,
                    proc.ExitCode);

            return new PgRestoreIsolatedRestoreOutcome
            {
                Success = restoreSucceeded,
                ExitCode = proc.ExitCode,
                StdErrSnippet = Truncate(CombineOut(stderr, stdout), 3500),
                DatabaseName = newDatabaseName
            };
        }
        finally
        {
            if (decryptedTempPath != null)
            {
                try
                {
                    if (File.Exists(decryptedTempPath))
                        File.Delete(decryptedTempPath);
                }
                catch
                {
                    // best-effort temp cleanup
                }
            }

            if (!databaseCreated)
            {
                // nothing to drop
            }
            else
            {
                var shouldDrop = !restoreSucceeded || dropEphemeralDatabaseAfterRestore;
                if (shouldDrop)
                {
                    try
                    {
                        await using var conn = new NpgsqlConnection(adminCs);
                        await conn.OpenAsync(cancellationToken);
                        await DropDatabaseIfExistsAsync(conn, newDatabaseName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ephemeral database drop failed: {Database}", newDatabaseName);
                    }
                }
            }
        }
    }

    public async Task DropEphemeralDatabaseAsync(
        string adminConnectionString,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminConnectionString))
            throw new ArgumentException("Admin connection string is required.", nameof(adminConnectionString));

        NpgsqlConnectionStringBuilder adminB;
        try
        {
            adminB = new NpgsqlConnectionStringBuilder(adminConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DropEphemeralDatabase: invalid admin connection string");
            return;
        }

        var maintenanceDb = string.IsNullOrWhiteSpace(adminB.Database) ? "postgres" : adminB.Database;
        adminB.Database = maintenanceDb;
        var adminCs = adminB.ConnectionString;

        try
        {
            await using var conn = new NpgsqlConnection(adminCs);
            await conn.OpenAsync(cancellationToken);
            await DropDatabaseIfExistsAsync(conn, databaseName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ephemeral database drop failed: {Database}", databaseName);
        }
    }

    private static PgRestoreIsolatedRestoreOutcome Fail(string db, int code, string msg) =>
        new()
        {
            Success = false,
            ExitCode = code,
            StdErrSnippet = msg,
            DatabaseName = db
        };

    private static async Task DropDatabaseIfExistsAsync(
        NpgsqlConnection conn,
        string dbName,
        CancellationToken ct)
    {
        await using var term = new NpgsqlCommand(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @name AND pid <> pg_backend_pid()
            """,
            conn);
        term.Parameters.AddWithValue("name", dbName);
        await term.ExecuteNonQueryAsync(ct);

        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{dbName}\"",
            conn);
        await drop.ExecuteNonQueryAsync(ct);
    }

    private static void TryKill(Process proc)
    {
        try
        {
            proc.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        if (value.Contains('"', StringComparison.Ordinal))
            throw new ArgumentException("Argument contains double-quote.", nameof(value));
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
    }

    private static string CombineOut(StringBuilder err, StringBuilder @out)
    {
        var e = err.ToString().Trim();
        var o = @out.ToString().Trim();
        if (e.Length == 0)
            return o.Length == 0 ? "(no output)" : o;
        if (o.Length == 0)
            return e;
        return $"stderr:\n{e}\nstdout:\n{o}";
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..max] + "…";
    }
}
