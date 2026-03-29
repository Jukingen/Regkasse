using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupRunQueryService : IBackupRunQueryService
{
    private readonly AppDbContext _db;

    public BackupRunQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BackupRun?> GetLatestRunAsync(CancellationToken cancellationToken = default)
    {
        return await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .Include(r => r.Verifications)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BackupRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .Include(r => r.Verifications)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<BackupRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.BackupRuns.AsNoTracking().OrderByDescending(r => r.RequestedAt);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<BackupVerification?> GetLatestVerificationAsync(CancellationToken cancellationToken = default)
    {
        return await _db.BackupVerifications.AsNoTracking()
            .OrderByDescending(v => v.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
