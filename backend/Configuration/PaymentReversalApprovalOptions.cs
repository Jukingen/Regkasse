namespace KasseAPI_Final.Configuration;

/// <summary>High-risk payment cancel/refund manager approval workflow.</summary>
public sealed class PaymentReversalApprovalOptions
{
    public const string SectionName = "PaymentReversalApproval";

    public bool Enabled { get; set; } = true;

    /// <summary>Cancel/refund amounts at or above this EUR threshold require manager approval.</summary>
    public decimal HighRiskAmountThresholdEur { get; set; } = 100m;

    /// <summary>Partial refunds above this share of the original total require approval (0–1).</summary>
    public decimal HighRiskRefundShareThreshold { get; set; } = 0.5m;

    public int ApprovalTokenTtlMinutes { get; set; } = 15;

    /// <summary>Offline-origin payments always require approval when enabled.</summary>
    public bool OfflineOriginAlwaysRequiresApproval { get; set; } = true;

    /// <summary>Storno count at or above this value within the window requires approval.</summary>
    public int HighRiskStornoCountThreshold { get; set; } = 5;

    public int HighRiskStornoWindowHours { get; set; } = 1;

    /// <summary>Payments older than this many hours require approval.</summary>
    public int HighRiskPaymentAgeHours { get; set; } = 24;

    public bool StornoFrequencyRequiresApproval { get; set; } = true;

    public bool PaymentOlderThanThresholdRequiresApproval { get; set; } = true;

    public string[] FallbackApproverEmails { get; set; } = Array.Empty<string>();
}
