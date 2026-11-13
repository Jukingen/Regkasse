using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.License;

public interface ILicenseRenewalFunnelService
{
    Task<LicenseRenewalFunnelDto> GetFunnelAsync(
        LicenseRenewalFunnelQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a renewal UI page/modal view for the ambient tenant (deduped per UTC day).
    /// </summary>
    Task<bool> RecordPageViewAsync(
        Guid tenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
