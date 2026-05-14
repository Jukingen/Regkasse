using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Production-facing <see cref="ILicenseService"/> adapter that delegates all operations to the
/// process-wide <see cref="LicenseService"/> singleton (trial, offline JWT, remote validation, revocation overlay).
/// </summary>
/// <remarks>
/// <para>
/// In non-development hosting this type is registered as <strong>scoped</strong> so <see cref="ILicenseService"/>
/// can be resolved per HTTP request; all authoritative state remains on the inner singleton <see cref="LicenseService"/>.
/// </para>
/// <para>
/// During OpenAPI export, the same adapter is registered as a <strong>singleton</strong> so <see cref="ILicenseService"/>
/// can be resolved when no request scope exists.
/// </para>
/// </remarks>
public sealed class ProductionLicenseService : ILicenseService
{
    private readonly LicenseService _inner;

    /// <summary>Creates an adapter around the shared <see cref="LicenseService"/> implementation.</summary>
    /// <param name="inner">The singleton license evaluator.</param>
    public ProductionLicenseService(LicenseService inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public void EvaluateOnStartup() => _inner.EvaluateOnStartup();

    /// <inheritdoc />
    public LicenseStatusResponse GetStatus() => _inner.GetStatus();

    /// <inheritdoc />
    public bool IsLicenseSnapshotInitialized => _inner.IsLicenseSnapshotInitialized;

    /// <inheritdoc />
    public Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default) =>
        _inner.ValidateAsync(cancellationToken);

    /// <inheritdoc />
    public Task<LicenseActivationResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default) =>
        _inner.ActivateAsync(request, clientInfo, cancellationToken);
}
