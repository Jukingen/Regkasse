namespace KasseAPI_Final.Services.Backup;

public interface IRestoreReportService
{
    /// <summary>
    /// Builds an RKSV-oriented restore report for a manual restore request id.
    /// Returns null when the request does not exist.
    /// </summary>
    Task<RestoreReport?> GenerateRestoreReportAsync(Guid restoreId, CancellationToken ct = default);
}
