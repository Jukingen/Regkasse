using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// TSE signing-certificate lifecycle: inspect, validate, sync metadata, schedule renewal.
/// Vendor cloud renew (e.g. fiskaly) is not implemented — renew syncs from <c>ITseKeyProvider</c>
/// when material is available, otherwise schedules an ops renewal target.
/// </summary>
public interface ITseCertificateService
{
    Task<TseCertificateInfoDto?> GetCertificateInfoAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<TseCertificateValidationResultDto> ValidateCertificateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<TseCertificateRenewalResultDto> RenewCertificateAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseCertificateRenewalResultDto> ScheduleCertificateRenewalAsync(
        Guid deviceId,
        DateTime renewalDateUtc,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Publish expiry warnings for devices approaching / past certificate expiry.</summary>
    Task ProcessExpiryWarningsAsync(CancellationToken cancellationToken = default);
}
