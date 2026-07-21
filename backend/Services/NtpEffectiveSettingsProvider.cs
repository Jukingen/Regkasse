using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class NtpEffectiveSettingsProvider : INtpEffectiveSettingsProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NtpSettings> _defaults;

    public NtpEffectiveSettingsProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<NtpSettings> defaults)
    {
        _scopeFactory = scopeFactory;
        _defaults = defaults;
    }

    public async Task<NtpSettings> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var d = _defaults.Value;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.NtpAdminSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return d;

        return new NtpSettings
        {
            Enabled = row.AutoSyncEnabled,
            DevelopmentBypass = d.DevelopmentBypass,
            SyncIntervalMinutes = Math.Clamp(row.SyncIntervalMinutes, 1, 24 * 60),
            MaxAllowedOffsetSeconds = Math.Clamp(row.MaxAllowedOffsetSeconds, 1, 3600),
            CriticalOffsetSeconds = Math.Clamp(row.CriticalOffsetSeconds, 1, 86400),
            NtpServers = d.NtpServers ?? Array.Empty<string>()
        };
    }
}
