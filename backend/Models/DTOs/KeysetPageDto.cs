namespace KasseAPI_Final.Models.DTOs;

/// <summary>Shared keyset pagination metadata for admin list endpoints.</summary>
public class KeysetPageMetaDto
{
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    /// <summary>Null when total was not computed (deep keyset pages).</summary>
    public int? TotalCount { get; set; }
    public int PageSize { get; set; }
}
