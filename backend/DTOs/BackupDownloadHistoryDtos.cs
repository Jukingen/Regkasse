namespace KasseAPI_Final.DTOs;

public sealed class BackupDownloadHistoryItemDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string? UserDisplayName { get; init; }
    public string? UserEmail { get; init; }
    public DateTime DownloadedAt { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string? IpAddress { get; init; }
}

public sealed class BackupDownloadHistoryResponseDto
{
    public Guid RunId { get; init; }
    public int DownloadCount { get; init; }
    public IReadOnlyList<BackupDownloadHistoryItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
