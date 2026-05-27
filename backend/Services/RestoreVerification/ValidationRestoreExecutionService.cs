using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Validation-only restore: ephemeral database, pg_restore, SQL validation, guaranteed cleanup.
/// </summary>
public sealed class ValidationRestoreExecutionService : IValidationRestoreExecutionService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly IPgRestoreIsolatedRestoreRunner _pgRestore;
    private readonly IPostRestoreDrillSqlChecker _continuityChecker;
    private readonly IFiscalGoLiveValidationRunner _fiscalValidation;
    private readonly ManualRestoreTargetDatabaseGuard _targetGuard;
    private readonly ILogger<ValidationRestoreExecutionService> _logger;

    public ValidationRestoreExecutionService(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<BackupOptions> backupOptions,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        IPgRestoreIsolatedRestoreRunner pgRestore,
        IPostRestoreDrillSqlChecker continuityChecker,
        IFiscalGoLiveValidationRunner fiscalValidation,
        ManualRestoreTargetDatabaseGuard targetGuard,
        ILogger<ValidationRestoreExecutionService> logger)
    {
        _db = db;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _backupOptions = backupOptions;
        _restoreOptions = restoreOptions;
        _pgRestore = pgRestore;
        _continuityChecker = continuityChecker;
        _fiscalValidation = fiscalValidation;
        _targetGuard = targetGuard;
        _logger = logger;
    }

    public async Task<RestoreResult> ExecuteValidationRestoreAsync(
        ValidationRestoreExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.ValidationOnly)
            return RestoreResult.Fail("ValidationOnly must be true; production restore is not supported.");

        var backupRun = await _db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.BackupRunId, cancellationToken);
        if (backupRun == null)
            return RestoreResult.Fail("Backup run not found.");
        if (backupRun.Status != BackupRunStatus.Succeeded)
            return RestoreResult.Fail($"Backup run must be Succeeded (current: {backupRun.Status}).");

        var dump = await ResolveDumpAsync(request.BackupRunId, cancellationToken);
        if (dump == null)
            return RestoreResult.Fail("Backup artifact not found on disk.");

        var restoreOpts = _restoreOptions.CurrentValue;
        if (!restoreOpts.IsolatedPgRestoreEnabled)
            return RestoreResult.Fail("IsolatedPgRestoreEnabled is false.");

        var isoConnName = restoreOpts.IsolatedRestoreAdminConnectionStringName?.Trim();
        if (string.IsNullOrEmpty(isoConnName))
            return RestoreResult.Fail("IsolatedRestoreAdminConnectionStringName is not configured.");

        if (_hostEnvironment.IsProduction()
            && string.Equals(isoConnName, "DefaultConnection", StringComparison.OrdinalIgnoreCase))
        {
            return RestoreResult.Fail(
                "Isolated restore admin connection must not be DefaultConnection in Production.");
        }

        var adminCs = _configuration.GetConnectionString(isoConnName);
        if (string.IsNullOrWhiteSpace(adminCs))
            return RestoreResult.Fail("Isolated restore admin connection string is missing.");

        var tempDbName = string.IsNullOrWhiteSpace(request.TargetDatabaseName)
            ? $"restore_validation_{Guid.NewGuid():N}"
            : request.TargetDatabaseName.Trim().ToLowerInvariant();

        try
        {
            _targetGuard.ValidateOrThrow(tempDbName);
        }
        catch (ArgumentException ex)
        {
            return RestoreResult.Fail(ex.Message);
        }

        var timeoutSec = restoreOpts.IsolatedPgRestoreTimeoutSeconds <= 0
            ? 3600
            : restoreOpts.IsolatedPgRestoreTimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(Math.Max(60, timeoutSec));

        try
        {
            _logger.LogInformation(
                "Validation restore starting: backupRunId={BackupRunId}, targetDb={TargetDb}",
                request.BackupRunId,
                tempDbName);

            var restoreOutcome = await _pgRestore.RestoreCustomDumpToEphemeralDatabaseAsync(
                adminCs,
                dump.Value.absolutePath,
                tempDbName,
                restoreOpts.PgRestoreExecutablePath,
                timeout,
                dropEphemeralDatabaseAfterRestore: false,
                cancellationToken);

            if (!restoreOutcome.Success)
            {
                return RestoreResult.Fail(
                    $"Restore failed: {restoreOutcome.StdErrSnippet ?? "pg_restore exit non-zero."}");
            }

            var targetCs = PostRestoreDrillSqlChecker.BuildTargetDatabaseConnectionString(adminCs, tempDbName);

            PostRestoreDrillSqlOutcome continuity;
            if (restoreOpts.PostRestoreSqlChecksEnabled)
            {
                continuity = await _continuityChecker.RunContinuityChecksAsync(targetCs, cancellationToken);
                if (!continuity.Executed || !continuity.Passed)
                {
                    return RestoreResult.Fail(
                        continuity.ErrorDetail ?? "Post-restore continuity validation failed.");
                }
            }
            else
            {
                continuity = new PostRestoreDrillSqlOutcome
                {
                    Executed = false,
                    Passed = true,
                    Checks = Array.Empty<PostRestoreSqlCheckRow>()
                };
            }

            bool? fiscalPassed = null;
            int? fiscalFail = null;
            int? fiscalWarn = null;
            if (restoreOpts.PostRestoreSqlChecksEnabled)
            {
                var scriptPath = ResolveFiscalScriptPath(restoreOpts);
                if (scriptPath != null && File.Exists(scriptPath))
                {
                    var fiscal = await _fiscalValidation.RunScriptAsync(scriptPath, targetCs, cancellationToken);
                    fiscalPassed = fiscal.Passed;
                    fiscalFail = fiscal.FailCount;
                    fiscalWarn = fiscal.WarnCount;
                    if (fiscal.Executed && !fiscal.Passed)
                    {
                        return RestoreResult.Fail(
                            fiscal.ErrorDetail ?? "Fiscal validation SQL reported failures on restored database.");
                    }
                }
            }

            var (tableCounts, rowCounts) = BuildCountMaps(continuity.Checks);

            return RestoreResult.Ok(new ValidationRestoreSummary
            {
                BackupRunId = request.BackupRunId,
                TargetDatabaseName = tempDbName,
                TableCounts = tableCounts,
                RowCounts = rowCounts,
                IntegrityChecksPassed = continuity.Passed,
                FiscalValidationPassed = fiscalPassed,
                FiscalValidationFailCount = fiscalFail,
                FiscalValidationWarnCount = fiscalWarn
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation restore failed for backupRunId={BackupRunId}", request.BackupRunId);
            return RestoreResult.Fail($"Validation failed: {ex.Message}");
        }
        finally
        {
            try
            {
                await _pgRestore.DropEphemeralDatabaseAsync(adminCs, tempDbName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Validation restore cleanup: failed to drop database {TargetDb}",
                    tempDbName);
            }
        }
    }

    private async Task<(Guid backupRunId, Guid artifactId, string absolutePath, string relativeDescriptor)?> ResolveDumpAsync(
        Guid backupRunId,
        CancellationToken cancellationToken)
    {
        var backupOpts = _backupOptions.CurrentValue;
        return await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
            _db,
            backupOpts,
            new[] { backupRunId },
            _logger,
            _hostEnvironment,
            cancellationToken);
    }

    private string? ResolveFiscalScriptPath(RestoreVerificationOptions restoreOpts)
    {
        var scriptRel = restoreOpts.FiscalValidationScriptRelativePath;
        if (string.IsNullOrWhiteSpace(scriptRel))
            return null;
        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, scriptRel));
    }

    private static (IReadOnlyDictionary<string, long> TableCounts, IReadOnlyDictionary<string, long> RowCounts)
        BuildCountMaps(IReadOnlyList<PostRestoreSqlCheckRow> checks)
    {
        var tableCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var rowCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var check in checks)
        {
            if (check.MeasuredValue is not long measured)
                continue;

            if (check.Category is "fiscal_spine" or "continuity_resilience" or "platform")
            {
                tableCounts[check.Id] = measured;
                rowCounts[check.Name] = measured;
            }
        }

        return (tableCounts, rowCounts);
    }
}
