using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
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

    public Task<RestoreVerificationRun?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        GetLatestAsync(access: null, cancellationToken);

    public async Task<RestoreVerificationRun?> GetLatestAsync(
        RestoreVerificationAccessScope? access,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAccess(BaseQuery(), access)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RestoreVerificationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, access: null, cancellationToken);

    public async Task<RestoreVerificationRun?> GetByIdAsync(
        Guid id,
        RestoreVerificationAccessScope? access,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAccess(BaseQuery(), access)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<(IReadOnlyList<RestoreVerificationRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        GetHistoryAsync(page, pageSize, filter: null, access: null, cancellationToken);

    public async Task<(IReadOnlyList<RestoreVerificationRun> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        RestoreVerificationHistoryFilter? filter,
        RestoreVerificationAccessScope? access,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = ApplyFilters(ApplyAccess(BaseQuery(), access), filter)
            .OrderByDescending(r => r.RequestedAt);

        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    private IQueryable<RestoreVerificationRun> BaseQuery() =>
        _db.RestoreVerificationRuns.AsNoTracking().Include(r => r.SourceBackupRun);

    private static IQueryable<RestoreVerificationRun> ApplyAccess(
        IQueryable<RestoreVerificationRun> q,
        RestoreVerificationAccessScope? access)
    {
        if (access is null || access.IsSuperAdmin)
            return q;

        if (access.CallerTenantId is null)
            return q.Where(_ => false);

        var tenantId = access.CallerTenantId.Value;
        return q.Where(r =>
            r.SourceBackupRun != null
            && r.SourceBackupRun.Strategy == BackupStrategyKind.Tenant
            && r.SourceBackupRun.TenantId == tenantId);
    }

    private static IQueryable<RestoreVerificationRun> ApplyFilters(
        IQueryable<RestoreVerificationRun> q,
        RestoreVerificationHistoryFilter? filter)
    {
        if (filter is null)
            return q;

        if (filter.Status.HasValue)
            q = q.Where(r => r.Status == filter.Status.Value);

        if (filter.TriggerSource.HasValue)
            q = q.Where(r => r.TriggerSource == filter.TriggerSource.Value);

        if (filter.FromUtc.HasValue)
            q = q.Where(r => r.RequestedAt >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            q = q.Where(r => r.RequestedAt <= filter.ToUtc.Value);

        var search = filter.SourceBackupRunIdSearch?.Trim();
        if (string.IsNullOrEmpty(search))
            return q;

        if (Guid.TryParse(search, out var exactId))
            return q.Where(r => r.SourceBackupRunId == exactId);

        if (search.Length < 8)
            return q.Where(_ => false);

        var prefix = search.ToLowerInvariant();
        return q.Where(r =>
            r.SourceBackupRunId != null
            && r.SourceBackupRunId.Value.ToString().ToLower().StartsWith(prefix));
    }
}
