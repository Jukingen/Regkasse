using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class NtpEffectiveSettingsProvider : INtpEffectiveSettingsProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptions<NtpSettings> _defaults;

    public NtpEffectiveSettingsProvider(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<NtpSettings> defaults)
    {
        _dbFactory = dbFactory;
        _defaults = defaults;
    }

    public async Task<NtpSettings> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var d = _defaults.Value;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.NtpAdminSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == NtpAdminSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return d;

        return new NtpSettings
        {
            Enabled = row.AutoSyncEnabled,
            SyncIntervalMinutes = Math.Clamp(row.SyncIntervalMinutes, 1, 24 * 60),
            MaxAllowedOffsetSeconds = Math.Clamp(row.MaxAllowedOffsetSeconds, 1, 3600),
            CriticalOffsetSeconds = Math.Clamp(row.CriticalOffsetSeconds, 1, 86400),
            NtpServers = d.NtpServers ?? Array.Empty<string>()
        };
    }
}
