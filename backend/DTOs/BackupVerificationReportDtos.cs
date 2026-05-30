using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public sealed class BackupVerificationReportDto
{
    [JsonPropertyName("backupRunId")]
    public Guid BackupRunId { get; init; }

    [JsonPropertyName("generatedAtUtc")]
    public DateTime GeneratedAtUtc { get; init; }

    [JsonPropertyName("backupCompletedAtUtc")]
    public DateTime? BackupCompletedAtUtc { get; init; }

    [JsonPropertyName("artifactCount")]
    public int ArtifactCount { get; init; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; init; }

    [JsonPropertyName("totalSizeFormatted")]
    public string TotalSizeFormatted { get; init; } = string.Empty;

    [JsonPropertyName("logicalDumpAnalyzed")]
    public bool LogicalDumpAnalyzed { get; init; }

    [JsonPropertyName("logicalDumpAnalysisMessage")]
    public string? LogicalDumpAnalysisMessage { get; init; }

    [JsonPropertyName("tableStatistics")]
    public IReadOnlyList<BackupTableStatisticsDto> TableStatistics { get; init; } = Array.Empty<BackupTableStatisticsDto>();

    [JsonPropertyName("sourceStatistics")]
    public BackupSourceDatabaseStatisticsDto? SourceStatistics { get; init; }

    [JsonPropertyName("verificationScore")]
    public int VerificationScore { get; init; }

    /// <summary><c>Verified</c>, <c>PartiallyVerified</c>, or <c>NotVerified</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "NotVerified";
}

public sealed class BackupTableStatisticsDto
{
    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = "public";

    [JsonPropertyName("tableName")]
    public string TableName { get; init; } = string.Empty;

    [JsonPropertyName("rowCount")]
    public long RowCount { get; init; }

    [JsonPropertyName("estimatedSizeBytes")]
    public long EstimatedSizeBytes { get; init; }

    [JsonPropertyName("presentInLogicalDump")]
    public bool PresentInLogicalDump { get; init; }

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; init; }

    [JsonPropertyName("verificationMessage")]
    public string? VerificationMessage { get; init; }
}

public sealed class BackupSourceDatabaseStatisticsDto
{
    [JsonPropertyName("analyzedAtUtc")]
    public DateTime AnalyzedAtUtc { get; init; }

    [JsonPropertyName("tables")]
    public IReadOnlyList<BackupTableRowCountDto> Tables { get; init; } = Array.Empty<BackupTableRowCountDto>();

    [JsonPropertyName("totalRowCount")]
    public long TotalRowCount { get; init; }
}

public sealed class BackupTableRowCountDto
{
    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = "public";

    [JsonPropertyName("tableName")]
    public string TableName { get; init; } = string.Empty;

    [JsonPropertyName("rowCount")]
    public long RowCount { get; init; }

    [JsonPropertyName("estimatedSizeBytes")]
    public long EstimatedSizeBytes { get; init; }

    [JsonPropertyName("tableExists")]
    public bool TableExists { get; init; }
}
