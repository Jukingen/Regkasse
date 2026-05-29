namespace KasseAPI_Final.Models.DTOs;

public sealed class DemoTemplateValidationIssueDto
{
    public int? Row { get; set; }
    public string Severity { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
}

public sealed class DemoTemplatePreviewRowDto
{
    public int Row { get; set; }
    public string RowType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? TaxRate { get; set; }
    public int? SortOrder { get; set; }
    public decimal? VatRate { get; set; }
}

public sealed class DemoTemplateValidationResultDto
{
    public bool IsValid { get; set; }
    public string? ParseError { get; set; }
    public int CategoryCount { get; set; }
    public int ProductCount { get; set; }
    public int TotalRows { get; set; }
    public List<DemoTemplateValidationIssueDto> Issues { get; set; } = [];
    public List<DemoTemplatePreviewRowDto> PreviewRows { get; set; } = [];
}
