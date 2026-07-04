using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupRunTenantAccessService : IBackupRunTenantAccessService
{
    private readonly AppDbContext _db;

    public BackupRunTenantAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BackupRun?> TryGetAccessibleRunAsync(
        Guid backupRunId,
        bool isSuperAdmin,
        Guid? callerTenantId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == backupRunId, cancellationToken);
        if (run == null)
            return null;

        if (isSuperAdmin && !callerTenantId.HasValue)
            return run;

        if (!callerTenantId.HasValue)
            return null;

        return BackupRunTenantSlugResolver.MatchesTenantHint(run, callerTenantId.Value) ? run : null;
    }
}
