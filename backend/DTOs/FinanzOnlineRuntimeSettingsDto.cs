namespace KasseAPI_Final.DTOs;

public sealed class FinanzOnlineRuntimeSettingsDto
{
    public bool UseSimulation { get; set; }
    public bool ConfigUseSimulation { get; set; }
    public bool EnableRealTestSubmission { get; set; }
    public bool ConfigEnableRealTestSubmission { get; set; }
    public bool EnableRealTestQuery { get; set; }
    public bool ConfigEnableRealTestQuery { get; set; }
    public bool RetryJobEnabled { get; set; }
    public bool ConfigRetryJobEnabled { get; set; }
    public FinanzOnlineOutboxWorkerNumericDto RetryIntervalSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerNumericDto RetryMaxRetryCount { get; set; } = new();
    public FinanzOnlineOutboxWorkerNumericDto RetryBaseDelaySeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerNumericDto RetryBackoffCapSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerNumericDto RetryBatchSize { get; set; } = new();
    public FinanzOnlineRuntimeAllowedDto Allowed { get; set; } = new();
    public string Source { get; set; } = "config";
    public bool CanManage { get; set; }
    public bool IsProduction { get; set; }
}

public sealed class FinanzOnlineRuntimeAllowedDto
{
    public FinanzOnlineOutboxWorkerRangeDto RetryIntervalSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto RetryMaxRetryCount { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto RetryBaseDelaySeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto RetryBackoffCapSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto RetryBatchSize { get; set; } = new();
}

public sealed class UpdateFinanzOnlineRuntimeRequest
{
    public bool? UseSimulation { get; set; }
    public bool? EnableRealTestSubmission { get; set; }
    public bool? EnableRealTestQuery { get; set; }
    public bool? RetryJobEnabled { get; set; }
    public int? RetryIntervalSeconds { get; set; }
    public int? RetryMaxRetryCount { get; set; }
    public int? RetryBaseDelaySeconds { get; set; }
    public int? RetryBackoffCapSeconds { get; set; }
    public int? RetryBatchSize { get; set; }
    public bool ClearOverride { get; set; }
}
