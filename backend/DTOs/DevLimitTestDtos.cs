using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>Development-only: set a single named tenant cap.</summary>
public sealed class SetLimitRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MinLength(1)]
    public string LimitKey { get; set; } = string.Empty;

    [Required]
    public decimal Value { get; set; }
}

/// <summary>Named QA scenarios for <c>POST /api/dev/limits/scenario/trigger</c>.</summary>
public static class DevLimitScenarioNames
{
    public const string Near = "near";
    public const string At = "at";
    public const string Tiny = "tiny";
    public const string Reset = "reset";
}

/// <summary>Development-only: adjust caps relative to live usage so the next real action hits the gate.</summary>
public sealed class TriggerLimitScenarioRequest
{
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// <c>near</c> (~80% usage), <c>at</c> (cap = current, next create fails),
    /// <c>tiny</c> (cap = 1), <c>reset</c> (defaults).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Scenario { get; set; } = string.Empty;

    /// <summary>Optional camelCase key. When omitted, the scenario applies to all caps.</summary>
    [JsonPropertyName("limitKey")]
    public string? LimitKey { get; set; }
}
