using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface ITenantSessionPolicyService
{
    Task<TenantSessionPolicyDto> GetPolicyAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

public sealed class TenantSessionPolicyService : ITenantSessionPolicyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantSessionPolicyService(AppDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<TenantSessionPolicyDto> GetPolicyAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantAccessor.TenantId;
        if (!resolvedTenantId.HasValue)
            return Defaults();

        var row = await _db.SystemSettings
            .AsNoTracking()
            .Where(s => s.TenantId == resolvedTenantId.Value)
            .Select(s => new
            {
                s.SessionTimeoutMinutes,
                s.SessionWarningBeforeTimeoutMinutes,
                s.KeepCartAfterTimeout,
                s.SessionIdleTimeoutEnabled,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return Defaults();

        return new TenantSessionPolicyDto
        {
            SessionTimeoutMinutes = Clamp(row.SessionTimeoutMinutes, 5, 480),
            WarningBeforeTimeoutMinutes = Clamp(row.SessionWarningBeforeTimeoutMinutes, 1, 60),
            KeepCartAfterTimeout = row.KeepCartAfterTimeout,
            IdleTimeoutEnabled = row.SessionIdleTimeoutEnabled,
        };
    }

    private static TenantSessionPolicyDto Defaults() => new()
    {
        SessionTimeoutMinutes = 30,
        WarningBeforeTimeoutMinutes = 5,
        KeepCartAfterTimeout = true,
        IdleTimeoutEnabled = true,
    };

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
