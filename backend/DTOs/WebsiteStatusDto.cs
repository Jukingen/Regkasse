namespace KasseAPI_Final.DTOs;

/// <summary>
/// Public website/app working-hours status (customer-facing only).
/// Never use this payload to restrict POS or FA.
/// </summary>
public sealed class WebsiteStatusDto
{
    /// <summary>Restaurant is within today's local open window.</summary>
    public bool IsOpen { get; init; }

    /// <summary>New online orders may be accepted (open + not in stop-before-close).</summary>
    public bool CanOrder { get; init; }

    /// <summary>Customer-facing German status message.</summary>
    public string Message { get; init; } = "Heute geschlossen";

    /// <summary>Today's local open time (<c>HH:mm</c>), when scheduled.</summary>
    public string? OpenTime { get; init; }

    /// <summary>Today's local close time (<c>HH:mm</c>), when scheduled.</summary>
    public string? CloseTime { get; init; }

    /// <summary>True when today is a configured special/holiday override.</summary>
    public bool IsSpecial { get; init; }
}

/// <summary>
/// Today's special-day override from <c>WorkingHoursSettings.SpecialDays</c> JSON.
/// </summary>
public sealed class WebsiteSpecialDayDto
{
    public bool IsSpecial { get; init; }
    public bool IsClosed { get; init; }
    public string? Message { get; init; }
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }
    public string? Date { get; init; }
}
