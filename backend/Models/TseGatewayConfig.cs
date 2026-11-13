using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Load-balancing strategies for the operational TSE API gateway (not fiscal signing).</summary>
public static class TseLoadBalancingStrategies
{
    public const string RoundRobin = "RoundRobin";
    public const string LeastConnections = "LeastConnections";
    public const string Weighted = "Weighted";

    public static readonly string[] All =
    {
        RoundRobin,
        LeastConnections,
        Weighted,
    };

    public static bool IsValid(string? strategy) =>
        !string.IsNullOrWhiteSpace(strategy)
        && All.Any(s => string.Equals(s, strategy, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? strategy) =>
        All.FirstOrDefault(s => string.Equals(s, strategy, StringComparison.OrdinalIgnoreCase))
        ?? RoundRobin;
}

/// <summary>Supported gateway operations. Fiscal Sign is intentionally excluded.</summary>
public static class TseGatewayOperations
{
    /// <summary>Probe <c>ITseProvider.IsReadyAsync</c> via load-balanced routing.</summary>
    public const string HealthProbe = "HealthProbe";
}

/// <summary>
/// Singleton operational TSE API gateway settings (provider LB for probes / diagnostics).
/// Does not replace <see cref="Services.ITseProviderFactory"/> fiscal signing selection.
/// </summary>
[Table("tse_gateway_configs")]
public sealed class TseGatewayConfig
{
    /// <summary>Fixed singleton primary key.</summary>
    public static readonly Guid SingletonId = Guid.Parse("a11ce6a7-7e50-4a7e-9a7e-7e5000000001");

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = SingletonId;

    [Required]
    [MaxLength(32)]
    [Column("strategy")]
    public string Strategy { get; set; } = TseLoadBalancingStrategies.RoundRobin;

    [Column("health_check_interval_seconds")]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    [Column("timeout_ms")]
    public int TimeoutMs { get; set; } = 5000;

    [Column("retry_count")]
    public int RetryCount { get; set; } = 3;

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public ICollection<TseGatewayEndpoint> Endpoints { get; set; } = new List<TseGatewayEndpoint>();
}

/// <summary>Configured backend endpoint (provider + URL) for gateway routing.</summary>
[Table("tse_gateway_endpoints")]
public sealed class TseGatewayEndpoint
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("config_id")]
    public Guid ConfigId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    [Column("endpoint_url")]
    public string EndpointUrl { get; set; } = string.Empty;

    [Column("weight")]
    public int Weight { get; set; } = 1;

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [ForeignKey(nameof(ConfigId))]
    public TseGatewayConfig? Config { get; set; }
}
