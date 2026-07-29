namespace KasseAPI_Final.Services.FeatureFlags;

public interface IFeatureFlagService
{
    /// <summary>
    /// Effective flag: tenant override → global override → config default.
    /// Unknown flags default to <c>false</c>.
    /// </summary>
    bool IsEnabled(string featureName, string? tenantId = null);

    /// <summary>
    /// Persist override in <c>tenant_settings</c>.
    /// <paramref name="tenantId"/> null = global override.
    /// </summary>
    Task SetEnabledAsync(
        string featureName,
        bool enabled,
        string? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Remove override so config default applies again.</summary>
    Task ClearOverrideAsync(
        string featureName,
        string? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureFlagStatusDto>> GetStatusesAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagStatusDto
{
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public bool ConfigDefault { get; init; }
    public bool? OverrideValue { get; init; }
    public string Source { get; init; } = "config"; // config | global_override | tenant_override
    public string? TenantId { get; init; }
}

public sealed class SetFeatureFlagRequest
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    /// <summary>Optional mandant id; omit for global override.</summary>
    public string? TenantId { get; set; }
    /// <summary>When true, remove override instead of setting a value.</summary>
    public bool ClearOverride { get; set; }
}
