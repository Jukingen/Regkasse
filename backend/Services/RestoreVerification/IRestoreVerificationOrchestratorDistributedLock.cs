namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore drill orkestrasyonunu çoklu API örneğinde tekilleştirir. HTTP katmanında kullanılmaz.
/// </summary>
public interface IRestoreVerificationOrchestratorDistributedLock
{
    Task<(RestoreVerificationOrchestratorGateAttempt Attempt, IAsyncDisposable? Lease)> TryEnterExclusiveAsync(
        CancellationToken cancellationToken = default);
}
