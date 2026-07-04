using System.Diagnostics;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup.PgDump;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Phase 2: logical backup via <c>pg_dump -Fc</c> in a child process (worker only). Not WAL/PITR/basebackup.
/// </summary>
public sealed class PostgreSqlPgDumpBackupExecutionAdapter : IBackupExecutionAdapter
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IPgDumpProcessRunner _runner;
    private readonly IBackupManifestService _manifestService;
    private readonly IBackupChecksumService _checksumService;
    private readonly ILogger<PostgreSqlPgDumpBackupExecutionAdapter> _logger;

    public PostgreSqlPgDumpBackupExecutionAdapter(
        IConfiguration configuration,
        IOptionsMonitor<BackupOptions> options,
        IPgDumpProcessRunner runner,
        IBackupManifestService manifestService,
        IBackupChecksumService checksumService,
        ILogger<PostgreSqlPgDumpBackupExecutionAdapter> logger)
    {
        _configuration = configuration;
        _options = options;
        _runner = runner;
        _manifestService = manifestService;
        _checksumService = checksumService;
        _logger = logger;
    }

    public string AdapterKind => "PgDump";

    public async Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var opts = _options.CurrentValue;
        var root = opts.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            return Fail("MISSING_STAGING_ROOT", "Backup:ArtifactStagingRoot is not set.");
        }

        var rootFull = Path.GetFullPath(root.Trim());
        Directory.CreateDirectory(rootFull);

        var connName = string.IsNullOrWhiteSpace(opts.LogicalDumpConnectionStringName)
            ? "DefaultConnection"
            : opts.LogicalDumpConnectionStringName.Trim();
        var connStr = _configuration.GetConnectionString(connName);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            return Fail(
                "MISSING_CONNECTION_STRING",
                $"Connection string '{connName}' is not configured for logical dump.");
        }

        NpgsqlConnectionStringBuilder csb;
        try
        {
            csb = new NpgsqlConnectionStringBuilder(connStr);
        }
        catch (Exception ex)
        {
            return Fail("INVALID_CONNECTION_STRING", $"Cannot parse connection string '{connName}': {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(csb.Host))
            return Fail("INVALID_CONNECTION_STRING", "Connection string has no Host.");
        if (string.IsNullOrWhiteSpace(csb.Username))
            return Fail("INVALID_CONNECTION_STRING", "Connection string has no Username.");
        if (string.IsNullOrWhiteSpace(csb.Database))
            return Fail("INVALID_CONNECTION_STRING", "Connection string has no Database.");

        var password = csb.Password ?? string.Empty;
        var fileNameTimestamp = context.ArtifactFileNameTimestampUtc ?? DateTime.UtcNow;
        var fileName = BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(
            context.TenantSlugForFileName,
            fileNameTimestamp);
        var outputPath = Path.GetFullPath(Path.Combine(rootFull, fileName));

        if (!BackupPathGuard.IsPathUnderStagingRoot(outputPath, rootFull))
            return Fail("PATH_ESCAPE", "Resolved output path left staging root.");

        var timeout = TimeSpan.FromSeconds(Math.Max(60, opts.PgDumpTimeoutSeconds));
        var spec = new PgDumpProcessSpec
        {
            ExecutablePath = string.IsNullOrWhiteSpace(opts.PgDumpExecutablePath)
                ? "pg_dump"
                : opts.PgDumpExecutablePath.Trim(),
            Host = csb.Host,
            Port = csb.Port == 0 ? 5432 : csb.Port,
            User = csb.Username,
            Password = password,
            Database = csb.Database,
            OutputFilePath = outputPath,
            Timeout = timeout
        };

        _logger.LogInformation(
            "pg_dump starting: runId={RunId}, correlationId={CorrelationId}, database={Database}, host={Host}, timeoutSeconds={TimeoutSeconds}",
            context.BackupRunId,
            context.CorrelationId,
            csb.Database,
            csb.Host,
            (int)timeout.TotalSeconds);

        var run = await _runner.RunAsync(spec, context.CancellationToken);
        if (!run.Success)
        {
            var code = run.ExitCode switch
            {
                -2 => "PG_DUMP_TIMEOUT",
                -3 => "PG_DUMP_CANCELLED",
                _ => "PG_DUMP_FAILED"
            };
            var detail = CombineStdStreams(run.StdErr, run.StdOut);
            _logger.LogWarning(
                "pg_dump did not succeed: runId={RunId}, correlationId={CorrelationId}, exitCode={ExitCode}, errorCode={ErrorCode}, elapsedMs={ElapsedMs}",
                context.BackupRunId,
                context.CorrelationId,
                run.ExitCode,
                code,
                sw.ElapsedMilliseconds);
            return new BackupExecutionResult
            {
                Success = false,
                ErrorCode = code,
                ErrorDetail = Truncate(detail, 3500),
                Artifacts = Array.Empty<BackupArtifactDescriptor>()
            };
        }

        if (!File.Exists(outputPath))
        {
            return Fail("DUMP_FILE_MISSING", "pg_dump reported success but output file is missing.");
        }

        var fi = new FileInfo(outputPath);
        if (fi.Length == 0)
            return Fail("DUMP_FILE_EMPTY", "pg_dump produced a zero-byte file.");

        if (!PgDumpCustomFormatSanity.TryValidate(outputPath, out var formatReason))
        {
            _logger.LogWarning(
                "pg_dump output failed custom-format sanity: runId={RunId}, correlationId={CorrelationId}, reason={Reason}, byteLength={ByteLength}",
                context.BackupRunId,
                context.CorrelationId,
                formatReason,
                fi.Length);
            return Fail("DUMP_FORMAT_INVALID", formatReason ?? "Custom-format sanity check failed.");
        }

        var hex = await _checksumService.ComputeFileSha256HexAsync(outputPath, context.CancellationToken);
        var dumpRelativeName = fileName;
        var manifestRelativeName = BackupArtifactFileNameBuilder.BuildManifestFileName(
            context.TenantSlugForFileName,
            fileNameTimestamp);

        var manifestDoc = _manifestService.BuildLogicalPgDumpManifest(
            new BackupLogicalManifestInput(
                context.BackupRunId,
                csb.Database,
                csb.Host,
                fi.Length,
                hex,
                dumpRelativeName,
                manifestRelativeName));

        var manifestPath = Path.GetFullPath(Path.Combine(rootFull, manifestRelativeName));
        if (!BackupPathGuard.IsPathUnderStagingRoot(manifestPath, rootFull))
            return Fail("PATH_ESCAPE", "Resolved manifest path left staging root.");

        await File.WriteAllTextAsync(manifestPath, manifestDoc.JsonText, context.CancellationToken);

        if (!File.Exists(manifestPath))
            return Fail("MANIFEST_WRITE_FAILED", "Manifest path missing after write.");

        var manifestFi = new FileInfo(manifestPath);
        if (manifestFi.Length == 0)
            return Fail("MANIFEST_EMPTY", "Manifest file is zero-byte after write.");

        _logger.LogInformation(
            "pg_dump adapter completed: runId={RunId}, correlationId={CorrelationId}, dumpBytes={DumpBytes}, manifestBytes={ManifestBytes}, elapsedMs={ElapsedMs}",
            context.BackupRunId,
            context.CorrelationId,
            fi.Length,
            manifestFi.Length,
            sw.ElapsedMilliseconds);

        return new BackupExecutionResult
        {
            Success = true,
            Artifacts = new[]
            {
                new BackupArtifactDescriptor
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = dumpRelativeName,
                    ByteSize = fi.Length,
                    ContentHashSha256 = hex,
                    MetadataJson = JsonSerializer.Serialize(new { format = "custom", role = "logical_dump" }),
                    RequireOnDiskHashVerification = true
                },
                new BackupArtifactDescriptor
                {
                    ArtifactType = BackupArtifactType.VerificationManifest,
                    StorageDescriptor = manifestRelativeName,
                    ByteSize = manifestDoc.JsonText.Length,
                    ContentHashSha256 = manifestDoc.ContentSha256LowerHex,
                    MetadataJson = manifestDoc.JsonText,
                    RequireOnDiskHashVerification = true
                }
            }
        };
    }

    private static BackupExecutionResult Fail(string code, string detail) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorDetail = detail,
            Artifacts = Array.Empty<BackupArtifactDescriptor>()
        };

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..max] + "…";
    }

    private static string CombineStdStreams(string? stderr, string? stdout)
    {
        var e = (stderr ?? string.Empty).Trim();
        var o = (stdout ?? string.Empty).Trim();
        if (e.Length == 0)
            return o.Length == 0 ? "(no stderr or stdout captured)" : $"stdout:\n{o}";
        if (o.Length == 0)
            return $"stderr:\n{e}";
        return $"stderr:\n{e}\nstdout:\n{o}";
    }
}
