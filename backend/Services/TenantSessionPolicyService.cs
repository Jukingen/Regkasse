using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface ITenantSessionPolicyService
{
    Task<TenantSessionPolicyDto> GetPolicyAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

public sealed class TenantSessionPolicyService : ITenantSessionPolicyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly SessionPolicyOptions _options;

    public TenantSessionPolicyService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<SessionPolicyOptions> options)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _options = options.Value;
    }

    public async Task<TenantSessionPolicyDto> GetPolicyAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantAccessor.TenantId;
        if (!resolvedTenantId.HasValue)
            return FromPlatformDefaults();

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
            return FromPlatformDefaults();

        return new TenantSessionPolicyDto
        {
            MaxConcurrentSessions = Clamp(_options.MaxConcurrentSessions, 1, 100),
            SessionTimeoutMinutes = Clamp(row.SessionTimeoutMinutes, 5, 480),
            AllowMultipleDevices = _options.AllowMultipleDevices,
            WarningBeforeTimeoutMinutes = Clamp(row.SessionWarningBeforeTimeoutMinutes, 1, 60),
            KeepCartAfterTimeout = row.KeepCartAfterTimeout,
            IdleTimeoutEnabled = row.SessionIdleTimeoutEnabled,
        };
    }

    private TenantSessionPolicyDto FromPlatformDefaults() => new()
    {
        MaxConcurrentSessions = Clamp(_options.MaxConcurrentSessions, 1, 100),
        SessionTimeoutMinutes = Clamp(_options.SessionTimeoutMinutes, 5, 480),
        AllowMultipleDevices = _options.AllowMultipleDevices,
        WarningBeforeTimeoutMinutes = 5,
        KeepCartAfterTimeout = true,
        IdleTimeoutEnabled = true,
    };

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
