using KasseAPI_Final.Services.VerticalProfiles;

namespace KasseAPI_Final.Tests;

/// <summary>Existing controller tests keep their previous behavior; profile gates are covered separately.</summary>
internal sealed class PermissiveVerticalProfileGuard : IVerticalProfileGuard
{
    public Task<bool> IsFeatureEnabledAsync(string feature, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task EnforceAsync(string feature, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task EnforceEndpointAsync(string feature, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<bool> IsTaxiSurfaceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task EnforceTaxiSurfaceAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task EnforceMobileServicesAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
