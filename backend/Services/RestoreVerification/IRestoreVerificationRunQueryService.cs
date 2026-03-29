using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationRunQueryService
{
    Task<RestoreVerificationRun?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<RestoreVerificationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RestoreVerificationRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
