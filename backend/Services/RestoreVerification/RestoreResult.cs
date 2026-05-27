using System.Text.Json.Serialization;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public ValidationRestoreSummary? Summary { get; init; }

    public static RestoreResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static RestoreResult Ok(ValidationRestoreSummary summary) =>
        new() { Success = true, Summary = summary };
}

public sealed class ValidationRestoreSummary
{
    public Guid BackupRunId { get; init; }

    public string TargetDatabaseName { get; init; } = string.Empty;

    [JsonPropertyName("tableCounts")]
    public IReadOnlyDictionary<string, long> TableCounts { get; init; } =
        new Dictionary<string, long>();

    [JsonPropertyName("rowCounts")]
    public IReadOnlyDictionary<string, long> RowCounts { get; init; } =
        new Dictionary<string, long>();

    [JsonPropertyName("integrityChecksPassed")]
    public bool IntegrityChecksPassed { get; init; }

    [JsonPropertyName("fiscalValidationPassed")]
    public bool? FiscalValidationPassed { get; init; }

    public int? FiscalValidationFailCount { get; init; }

    public int? FiscalValidationWarnCount { get; init; }
}
