using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DataRetention;

public sealed class RksvColdArchiveResult
{
    public int EligibleCount { get; init; }
    public int ArchivedCount { get; init; }
    public Guid? ArchiveRunId { get; init; }
    public string? ArchivePath { get; init; }
    public bool HardDeleteRefused { get; init; }
    public string? Message { get; init; }
}

public interface IRksvDataCleanupService
{
    /// <summary>
    /// Cold-archives fiscal payments older than the configured retention window.
    /// Does not delete live RKSV rows (signature chain integrity).
    /// </summary>
    Task<RksvColdArchiveResult> CleanupRksvDataAsync(CancellationToken ct = default);
}
