using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

internal static class DevelopmentModeSettingsEnsure
{
    internal static async Task EnsureSingletonAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.DevelopmentModeSettings.AsNoTracking()
                .AnyAsync(x => x.Id == DevelopmentModeSettings.SingletonId, cancellationToken)
                .ConfigureAwait(false))
            return;

        db.DevelopmentModeSettings.Add(new DevelopmentModeSettings
        {
            Id = DevelopmentModeSettings.SingletonId,
            Enabled = false,
            BypassLicense = false,
            BypassNtpCheck = false,
            BypassTseCheck = false,
            SimulateOffline = false,
            ForceOnline = false,
            ValidDays = 365,
            Features = [],
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = null,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            if (!await db.DevelopmentModeSettings.AsNoTracking()
                    .AnyAsync(x => x.Id == DevelopmentModeSettings.SingletonId, cancellationToken)
                    .ConfigureAwait(false))
                throw;
        }
    }
}
