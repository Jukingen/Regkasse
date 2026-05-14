using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Development-only <see cref="ILicenseService"/> that keeps persistence and activation on the real
/// <see cref="LicenseService"/> while exposing a synthetic <strong>licensed</strong> snapshot so POS/FA and
/// payment guards stay unblocked during local testing. Not registered in Production.
/// </summary>
public sealed class DevelopmentLicenseService : ILicenseService
{
    private static int _bypassWarningLogged;

    private readonly LicenseService _inner;
    private readonly ILogger<DevelopmentLicenseService> _logger;

    /// <summary>Initializes the development decorator around the shared license evaluator.</summary>
    /// <param name="inner">Singleton used for startup evaluation, activation, and machine hash.</param>
    /// <param name="logger">Structured logging.</param>
    public DevelopmentLicenseService(LicenseService inner, ILogger<DevelopmentLicenseService> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    /// <inheritdoc />
    public void EvaluateOnStartup() => _inner.EvaluateOnStartup();

    /// <inheritdoc />
    public bool IsLicenseSnapshotInitialized => _inner.IsLicenseSnapshotInitialized;

    /// <inheritdoc />
    public Task<LicenseActivationResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default) =>
        _inner.ActivateAsync(request, clientInfo, cancellationToken);

    /// <inheritdoc />
    public async Task<LicenseStatusResponse> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        await _inner.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return GetStatus();
    }

    /// <inheritdoc />
    public LicenseStatusResponse GetStatus()
    {
        if (Interlocked.Exchange(ref _bypassWarningLogged, 1) == 0)
            _logger.LogWarning("⚠️ DEVELOPMENT MODE: License checks bypassed");

        var snapshot = _inner.GetStatus();
        var validUntil = DateTime.SpecifyKind(DateTime.UtcNow.AddYears(1), DateTimeKind.Utc);
        var daysRemaining = Math.Max(0, (int)Math.Ceiling((validUntil - DateTime.UtcNow).TotalDays));

        return new LicenseStatusResponse(
            IsValid: true,
            IsTrial: false,
            IsExpired: false,
            DaysRemaining: daysRemaining,
            ExpiryDate: validUntil,
            MachineHash: snapshot.MachineHash,
            snapshot.Reminders,
            LicenseFeatureIds.All);
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        await _inner.ValidateAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var s = GetStatus();
        return new LicenseValidationResult
        {
            IsLicenseOperational = true,
            IsTrial = false,
            IsExpired = false,
            IsPaidValid = true,
            DaysRemaining = s.DaysRemaining,
            ExpiryUtc = s.ExpiryDate,
        };
    }
}
