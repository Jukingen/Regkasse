namespace KasseAPI_Final.DTOs;

public sealed class AdminRksvRuntimeConfigValuesDto
{
    public string Mode { get; init; } = "Production";

    public string TseMode { get; init; } = "Real";

    public string FinanzOnlineMode { get; init; } = "Real";

    public bool ShowDemoLabel { get; init; }

    /// <summary>Development-only TSE health bypass overlay. Ignored when TseMode is Real or host is not Development.</summary>
    public bool BypassTseInDevelopment { get; init; }
}

public sealed class AdminRksvRuntimeConfigResponseDto
{
    public string Mode { get; init; } = "Production";

    public string TseMode { get; init; } = "Real";

    public string FinanzOnlineMode { get; init; } = "Real";

    public bool ShowDemoLabel { get; init; }

    /// <summary>Persisted overlay flag. Ignored when TseMode is Real or host is not Development.</summary>
    public bool BypassTseInDevelopment { get; init; }

    /// <summary>Effective TSE health bypass after overlay TseMode=Real and host-environment gates.</summary>
    public bool TseHealthBypassEffective { get; init; }

    /// <summary>True when overlay TseMode=Real disabled a Development bypass that would otherwise apply.</summary>
    public bool TseHealthBypassBlockedByRealTseMode { get; init; }

    /// <summary><c>database</c> after first persist; otherwise <c>appsettings</c>.</summary>
    public string Source { get; init; } = "appsettings";

    public bool OverlayPersisted { get; init; }

    public string HostEnvironment { get; init; } = string.Empty;

    public bool ProductionLockApplies { get; init; }

    public bool ProductionLockOk { get; init; } = true;

    public IReadOnlyList<string> ProductionLockReasons { get; init; } = Array.Empty<string>();

    /// <summary>Always false: overlay is applied in-process without restart.</summary>
    public bool RestartRequired { get; init; }

    /// <summary>True when this host allows Demo/Simulation overlays (Development, or Production with escape hatch).</summary>
    public bool CanSetDemoOnThisHost { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public string? UpdatedBy { get; init; }

    public AdminRksvRuntimeConfigValuesDto AppsettingsFallback { get; init; } = new();
}

public sealed class AdminRksvRuntimeConfigPostRequestDto
{
    public string Mode { get; set; } = "Production";

    public string TseMode { get; set; } = "Real";

    public string FinanzOnlineMode { get; set; } = "Real";

    public bool ShowDemoLabel { get; set; }

    /// <summary>Development-only TSE health bypass overlay. Ignored when TseMode is Real.</summary>
    public bool BypassTseInDevelopment { get; set; }

    /// <summary>Optional operator reason stored in audit metadata.</summary>
    public string? Reason { get; set; }
}
