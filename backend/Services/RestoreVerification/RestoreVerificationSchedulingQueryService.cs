using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationSchedulingQueryService : IRestoreVerificationSchedulingQueryService
{
    private readonly AppDbContext _db;

    public RestoreVerificationSchedulingQueryService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<DateTime?> GetLastSuccessfulScheduledProofCompletedAtUtcAsync(CancellationToken cancellationToken = default)
    {
        return _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.TriggerSource == RestoreVerificationTriggerSource.Scheduled
                        && r.Status == RestoreVerificationStatus.Succeeded
                        && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasActiveScheduledQueuedOrRunningAsync(CancellationToken cancellationToken = default)
    {
        return _db.RestoreVerificationRuns.AsNoTracking()
            .AnyAsync(
                r => r.TriggerSource == RestoreVerificationTriggerSource.Scheduled
                     && (r.Status == RestoreVerificationStatus.Queued
                         || r.Status == RestoreVerificationStatus.Running),
                cancellationToken);
    }
}
