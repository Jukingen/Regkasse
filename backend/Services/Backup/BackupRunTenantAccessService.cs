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
        string? callerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == backupRunId, cancellationToken);
        if (run == null)
            return null;

        var scope = new BackupRunAccessScope(isSuperAdmin, callerTenantId, callerUserId);
        return BackupRunAccessEvaluator.IsRunAccessible(run, scope) ? run : null;
    }
}
