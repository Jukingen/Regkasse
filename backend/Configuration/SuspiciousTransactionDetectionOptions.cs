namespace KasseAPI_Final.Configuration;

/// <summary>Periodic suspicious payment pattern detection (admin alerts + activity feed).</summary>
public sealed class SuspiciousTransactionDetectionOptions
{
    public const string SectionName = "SuspiciousTransactionDetection";

    public bool Enabled { get; set; } = true;

    public int ScanIntervalMinutes { get; set; } = 5;

    public decimal HighValueThresholdEur { get; set; } = 500m;

    public int HighValueLookbackMinutes { get; set; } = 30;

    public int MaxStornosPerHour { get; set; } = 5;

    public int MaxRefundsPerDay { get; set; } = 3;

    /// <summary>Same customer card payments within one hour before alerting.</summary>
    public int SameCardPaymentsPerHour { get; set; } = 3;

    /// <summary>Non-storno sales by one cashier within one hour.</summary>
    public int RapidTransactionsPerHour { get; set; } = 10;

    /// <summary>Local hour (Europe/Vienna) when unusual-time window starts (inclusive).</summary>
    public int UnusualHourStart { get; set; } = 22;

    /// <summary>Local hour (Europe/Vienna) when unusual-time window ends (exclusive).</summary>
    public int UnusualHourEnd { get; set; } = 6;

    public int UnusualTimeLookbackMinutes { get; set; } = 30;

    /// <summary>Skip re-alerting the same dedup key within this many hours.</summary>
    public int DedupWindowHours { get; set; } = 24;
}
