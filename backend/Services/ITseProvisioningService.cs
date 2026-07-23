using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Provisions TSE device rows and signature-chain state for tenants / cash registers.
/// Does not invent a parallel signing stack — uses existing <see cref="TseDevice"/> + <see cref="SignatureChainState"/>.
/// </summary>
public interface ITseProvisioningService
{
    /// <summary>
    /// Provisions TSE for the tenant's default (or first) cash register.
    /// When <see cref="TseOptions.IsOff"/>, returns a skipped success without writing rows.
    /// </summary>
    /// <param name="force">
    /// When true (Super Admin manual provision), ignores <see cref="TseOptions.AutoProvisionOnTenantCreate"/>.
    /// </param>
    Task<TseProvisioningResult> ProvisionTseForTenantAsync(
        Guid tenantId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a TSE device row bound to <paramref name="cashRegisterId"/> (<c>KassenId</c>)
    /// and ensures <see cref="SignatureChainState"/> exists for that register.
    /// </summary>
    /// <param name="force">
    /// When true (Super Admin manual provision), ignores <see cref="TseOptions.AutoProvisionOnTenantCreate"/>.
    /// </param>
    Task<TseProvisioningResult> ProvisionTseForCashRegisterAsync(
        Guid cashRegisterId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>Aggregated TSE provisioning status for a tenant (device rows + chain + options).</summary>
    Task<TseProvisioningStatus> GetTseStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Lightweight readiness check for a tenant's provisioned TSE device(s).</summary>
    Task<TseProvisioningHealthCheck> PerformHealthCheckAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Cross-tenant TSE device inventory for Super Admin (via cash-register join).</summary>
    Task<IReadOnlyList<TseDeviceFleetItemDto>> ListDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Fleet summary + process-wide health snapshot (reuses <c>ITseHealthMonitor</c>).</summary>
    Task<TseFleetOverviewDto> GetFleetOverviewAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Soft-revokes a TSE device (deactivates; does not delete fiscal history).</summary>
    Task<TseProvisioningResult> RevokeTseDeviceAsync(
        Guid deviceId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of an automatic TSE provisioning attempt.</summary>
public enum TseProvisioningOutcome
{
    Success = 0,
    Skipped = 1,
    Failed = 2,
}

public sealed class TseProvisioningResult
{
    public TseProvisioningOutcome Outcome { get; init; }

    public bool IsSuccess => Outcome is TseProvisioningOutcome.Success or TseProvisioningOutcome.Skipped;

    public string? Error { get; init; }

    public TseDevice? Device { get; init; }

    public bool SignatureChainInitialized { get; init; }

    public bool StartbelegCreated { get; init; }

    public string? Detail { get; init; }

    public static TseProvisioningResult Fail(string error) => new()
    {
        Outcome = TseProvisioningOutcome.Failed,
        Error = error,
    };

    public static TseProvisioningResult Skipped(string detail) => new()
    {
        Outcome = TseProvisioningOutcome.Skipped,
        Detail = detail,
    };

    public static TseProvisioningResult Success(
        TseDevice device,
        bool signatureChainInitialized,
        string? detail = null,
        bool startbelegCreated = false) => new()
    {
        Outcome = TseProvisioningOutcome.Success,
        Device = device,
        SignatureChainInitialized = signatureChainInitialized,
        StartbelegCreated = startbelegCreated,
        Detail = detail,
    };
}

/// <summary>
/// Tenant-scoped TSE status for provisioning / onboarding (distinct from <see cref="TseStatus"/> device probe DTO).
/// </summary>
public sealed class TseProvisioningStatus
{
    public Guid TenantId { get; init; }

    public string TseMode { get; init; } = string.Empty;

    public string SigningMode { get; init; } = string.Empty;

    public bool IsOff { get; init; }

    public int DeviceCount { get; init; }

    public int ActiveDeviceCount { get; init; }

    public int ConnectedDeviceCount { get; init; }

    public int RegistersWithChainState { get; init; }

    public int CashRegisterCount { get; init; }

    public Guid? PrimaryDeviceId { get; init; }

    public string? PrimarySerialNumber { get; init; }

    public string? PrimaryDeviceType { get; init; }

    public bool IsOperational { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }
}

public sealed class TseProvisioningHealthCheck
{
    public Guid TenantId { get; init; }

    public bool IsHealthy { get; init; }

    public DateTime CheckedAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public Guid? DeviceId { get; init; }

    public string? SerialNumber { get; init; }

    public bool ProviderReady { get; init; }
}
