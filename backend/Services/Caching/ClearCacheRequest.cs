namespace KasseAPI_Final.Services.Caching;

/// <summary>Super Admin cache-clear request for troubleshooting.</summary>
public sealed class ClearCacheRequest
{
    /// <summary>When set, clears keys under <c>license_status_{tenantId}</c> and <c>product_list_{tenantId}</c>.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>When set, removes all keys starting with this prefix.</summary>
    public string? Prefix { get; set; }

    /// <summary>When true, clears the entire application cache tracked by <see cref="ICacheService"/>.</summary>
    public bool ClearAll { get; set; }
}

/// <summary>Result of a Super Admin cache clear operation.</summary>
public sealed class ClearCacheResult
{
    public bool Success { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string? Detail { get; init; }
}
