namespace KasseAPI_Final.Configuration;

/// <summary>
/// Options for the periodic payload_hash repair job. Runs until legacy payload hash is fully aligned (report-only conflict strategy).
/// </summary>
public sealed class PayloadHashRepairJobOptions
{
    public const string SectionName = "PayloadHashRepairJob";

    /// <summary>Enable periodic repair job. Default true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Interval between repair cycles. Default 01:00:00 (1 hour).</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Max rows to repair per cycle (per batch). Default 500.</summary>
    public int BatchSizePerCycle { get; set; } = 500;

    /// <summary>Max repair batches per cycle (stops when no updates or this many batches). Default 10.</summary>
    public int MaxBatchesPerCycle { get; set; } = 10;

    /// <summary>Sample size for completion metric (AnalyzeAsync after repair). Default 5000.</summary>
    public int CompletionSampleSize { get; set; } = 5000;
}
