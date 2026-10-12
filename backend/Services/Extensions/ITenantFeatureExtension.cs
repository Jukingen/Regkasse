namespace KasseAPI_Final.Services.Extensions;

/// <summary>
/// Additive tenant behavior. Core payment, receipt, customer, and folio services
/// call enabled extensions at named hooks. An extension must not replace fiscal signing.
/// </summary>
public interface ITenantFeatureExtension
{
    /// <summary>Unique extension name (used in tenant_settings).</summary>
    string FeatureName { get; }

    /// <summary>When this extension should run for a given tenant.</summary>
    bool AppliesTo(Guid tenantId);

    /// <summary>Execute the extension's logic.</summary>
    Task<ExtensionResult> ExecuteAsync(
        ExtensionContext context,
        CancellationToken ct);
}

public record ExtensionContext(
    Guid TenantId,
    string HookName,
    IReadOnlyDictionary<string, object?> Payload);

public record ExtensionResult(
    bool Success,
    string? Message = null,
    IReadOnlyDictionary<string, object?>? ModifiedPayload = null);
