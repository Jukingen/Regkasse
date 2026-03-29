using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationRunQueryService : IRestoreVerificationRunQueryService
{
    private readonly AppDbContext _db;

    public RestoreVerificationRunQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RestoreVerificationRun?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _db.RestoreVerificationRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RestoreVerificationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.RestoreVerificationRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<RestoreVerificationRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.RestoreVerificationRuns.AsNoTracking().OrderByDescending(r => r.RequestedAt);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }
}
