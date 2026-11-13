using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.OperationalRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationManualTriggerService : IRestoreVerificationManualTriggerService
{
    private const int IdempotencyKeyMaxLength = 200;
    private const int IdempotencyInsertRetryMax = 5;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly ILogger<RestoreVerificationManualTriggerService> _logger;

    public RestoreVerificationManualTriggerService(
        AppDbContext db,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        ILogger<RestoreVerificationManualTriggerService> logger)
    {
        _db = db;
        _restoreOptions = restoreOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RestoreVerificationManualTriggerResult> EnqueueManualAsync(
        string? requestedByUserId,
        string? correlationId,
        string? idempotencyKey,
        Guid? sourceBackupRunId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        Guid? pinnedBackupRunId = null;
        if (sourceBackupRunId.HasValue)
        {
            pinnedBackupRunId = await ResolveAndValidateSourceBackupRunIdAsync(
                    sourceBackupRunId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        for (var attempt = 0; attempt < IdempotencyInsertRetryMax; attempt++)
        {
            if (normalizedKey != null)
            {
                var byKey = await _db.RestoreVerificationRuns.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IdempotencyKey == normalizedKey, cancellationToken);
                if (byKey != null)
                {
                    _logger.LogInformation(
                        "Restore verification manual trigger: returning existing run for idempotency key. RunId={RunId}, Status={Status}",
                        byKey.Id,
                        byKey.Status);
                    return new RestoreVerificationManualTriggerResult
                    {
                        Run = byKey,
                        OrchestrationState = RestoreVerificationTriggerOrchestrationState.ExistingByIdempotencyKey
                    };
                }
            }

            var active = await _db.RestoreVerificationRuns.AsNoTracking()
                .Where(r => r.Status == RestoreVerificationStatus.Queued || r.Status == RestoreVerificationStatus.Running)
                .OrderBy(r => r.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (active != null)
            {
                _logger.LogInformation(
                    "Restore verification manual trigger: returning existing active run (dedupe). RunId={RunId}, Status={Status}",
                    active.Id,
                    active.Status);
                return new RestoreVerificationManualTriggerResult
                {
                    Run = active,
                    OrchestrationState = RestoreVerificationTriggerOrchestrationState.ExistingActiveRunReturned
                };
            }

            var ro = _restoreOptions.CurrentValue;
            var run = new RestoreVerificationRun
            {
                Status = RestoreVerificationStatus.Queued,
                TriggerSource = RestoreVerificationTriggerSource.Manual,
                RequestedAt = DateTime.UtcNow,
                RequestedByUserId = requestedByUserId,
                CorrelationId = correlationId,
                IdempotencyKey = normalizedKey,
                SourceBackupRunId = pinnedBackupRunId,
                ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeRestore(
                    ro,
                    "restore_manual_enqueue",
                    DateTime.UtcNow)
            };

            _db.RestoreVerificationRuns.Add(run);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Restore verification manual trigger: new run queued. RunId={RunId}, IdempotencyKeySet={HasKey}, SourceBackupRunId={SourceBackupRunId}",
                    run.Id,
                    normalizedKey != null,
                    pinnedBackupRunId);
                return new RestoreVerificationManualTriggerResult
                {
                    Run = run,
                    OrchestrationState = RestoreVerificationTriggerOrchestrationState.NewlyQueued
                };
            }
            catch (DbUpdateException ex) when (IsUniqueIdempotencyViolation(ex))
            {
                _db.ChangeTracker.Clear();
                _logger.LogWarning(
                    ex,
                    "Restore verification manual trigger: idempotency unique conflict, retrying lookup. Attempt={Attempt}",
                    attempt + 1);
            }
        }

        throw new InvalidOperationException(
            "Could not enqueue restore verification: idempotency key contention exceeded retry limit.");
    }

    /// <summary>
    /// Ensures the backup run exists, succeeded, and has a LogicalDump artifact (drill-eligible).
    /// </summary>
    private async Task<Guid> ResolveAndValidateSourceBackupRunIdAsync(
        Guid backupRunId,
        CancellationToken cancellationToken)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == backupRunId, cancellationToken)
            .ConfigureAwait(false);

        if (run == null)
            throw new KeyNotFoundException($"Backup run {backupRunId} not found.");

        if (run.Status != BackupRunStatus.Succeeded)
            throw new ArgumentException(
                $"Backup run {backupRunId} is not Succeeded (status={run.Status}); cannot pin restore drill.",
                nameof(backupRunId));

        var hasDump = run.Artifacts.Any(a => a.ArtifactType == BackupArtifactType.LogicalDump);
        if (!hasDump)
            throw new ArgumentException(
                $"Backup run {backupRunId} has no LogicalDump artifact; cannot pin restore drill.",
                nameof(backupRunId));

        return backupRunId;
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;
        var t = idempotencyKey.Trim();
        if (t.Length > IdempotencyKeyMaxLength)
            throw new ArgumentException(
                $"Idempotency key must be at most {IdempotencyKeyMaxLength} characters after trim.",
                nameof(idempotencyKey));
        return t;
    }

    private static bool IsUniqueIdempotencyViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation
                && pg.ConstraintName != null
                && pg.ConstraintName.Contains("idempotency", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
