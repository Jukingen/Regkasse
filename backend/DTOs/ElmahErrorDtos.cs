namespace KasseAPI_Final.DTOs;

public sealed class ElmahErrorListItemDto
{
    public Guid ErrorId { get; set; }
    public string Application { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? User { get; set; }
    public int StatusCode { get; set; }
    public DateTime TimeUtc { get; set; }
    public string? AllXml { get; set; }
}

public sealed class ElmahErrorListResponseDto
{
    public IReadOnlyList<ElmahErrorListItemDto> Items { get; set; } = Array.Empty<ElmahErrorListItemDto>();
    public int TotalCount { get; set; }
}
