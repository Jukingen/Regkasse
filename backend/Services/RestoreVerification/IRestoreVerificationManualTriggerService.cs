using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationManualTriggerService
{
    Task<RestoreVerificationRun> EnqueueManualAsync(
        string? requestedByUserId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
