namespace KasseAPI_Final.Models.DTOs;

/// <summary>Multipart upload: form field <c>file</c> (CSV or .xlsx). No JSON body.</summary>
public static class BulkImportRequest
{
    public const string FileFormFieldName = "file";
    public const int DefaultPreviewRowCount = 10;
}

/// <summary>Parsed row from import file (preview or processing).</summary>
public sealed class BulkImportPreviewRowDto
{
    public int Row { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}

public sealed class BulkImportPreviewResponseDto
{
    public int TotalRows { get; set; }
    public IReadOnlyList<BulkImportPreviewRowDto> PreviewRows { get; set; } = Array.Empty<BulkImportPreviewRowDto>();
    public string? ParseError { get; set; }
}

public sealed class BulkImportStartResponseDto
{
    public string JobId { get; set; } = string.Empty;
    public int TotalRows { get; set; }
}

/// <summary>Internal parsed row with source line number.</summary>
public class BulkImportRow
{
    public int RowNumber { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string Role { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
}
