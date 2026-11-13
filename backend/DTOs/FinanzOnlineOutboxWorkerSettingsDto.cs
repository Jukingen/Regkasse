namespace KasseAPI_Final.DTOs;

public sealed class FinanzOnlineOutboxWorkerSettingsDto
{
    /// <summary>Effective worker flag (config unless a Super Admin overlay exists).</summary>
    public bool Enabled { get; set; }

    public bool ConfigEnabled { get; set; }

    public bool? OverrideEnabled { get; set; }

    public FinanzOnlineOutboxWorkerNumericDto PollIntervalSeconds { get; set; } = new();

    public FinanzOnlineOutboxWorkerNumericDto MaxAttempts { get; set; } = new();

    public FinanzOnlineOutboxWorkerNumericDto BaseDelaySeconds { get; set; } = new();

    public FinanzOnlineOutboxWorkerNumericDto BackoffCapSeconds { get; set; } = new();

    public FinanzOnlineOutboxWorkerNumericDto JitterMaxSeconds { get; set; } = new();

    public FinanzOnlineOutboxWorkerNumericDto ProcessingTimeoutSeconds { get; set; } = new();

    public FinanzOnlineOutboxWorkerAllowedDto Allowed { get; set; } = new();

    /// <summary><c>config</c> or <c>global_override</c>.</summary>
    public string Source { get; set; } = "config";

    public bool CanManage { get; set; }

    public bool IsProduction { get; set; }
}

public sealed class FinanzOnlineOutboxWorkerNumericDto
{
    public int Effective { get; set; }
    public int Config { get; set; }
    public int? Overlay { get; set; }
}

public sealed class FinanzOnlineOutboxWorkerAllowedDto
{
    public FinanzOnlineOutboxWorkerRangeDto PollIntervalSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto MaxAttempts { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto BaseDelaySeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto BackoffCapSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto JitterMaxSeconds { get; set; } = new();
    public FinanzOnlineOutboxWorkerRangeDto ProcessingTimeoutSeconds { get; set; } = new();
}

public sealed class FinanzOnlineOutboxWorkerRangeDto
{
    public int Min { get; set; }
    public int Max { get; set; }
    public int[] Values { get; set; } = [];
}

public sealed class UpdateFinanzOnlineOutboxWorkerRequest
{
    /// <summary>When set, updates the Enabled overlay. Omitted on numeric-only saves.</summary>
    public bool? Enabled { get; set; }

    public int? PollIntervalSeconds { get; set; }
    public int? MaxAttempts { get; set; }
    public int? BaseDelaySeconds { get; set; }
    public int? BackoffCapSeconds { get; set; }
    public int? JitterMaxSeconds { get; set; }
    public int? ProcessingTimeoutSeconds { get; set; }

    /// <summary>When true, delete the overlay and follow <c>FinanzOnlineOutbox</c> from configuration.</summary>
    public bool ClearOverride { get; set; }

    /// <summary>Required when turning the worker off in Production.</summary>
    public bool ConfirmProductionDisable { get; set; }
}
