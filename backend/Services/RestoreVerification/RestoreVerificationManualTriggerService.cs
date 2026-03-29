using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationManualTriggerService : IRestoreVerificationManualTriggerService
{
    private readonly AppDbContext _db;

    public RestoreVerificationManualTriggerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RestoreVerificationRun> EnqueueManualAsync(
        string? requestedByUserId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = requestedByUserId,
            CorrelationId = correlationId
        };
        _db.RestoreVerificationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }
}
