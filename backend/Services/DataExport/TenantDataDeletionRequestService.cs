using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DataExport;

public interface ITenantDataDeletionRequestService
{
    Task<TenantDataDeletionRequestDto> RequestDeletionAsync(
        Guid tenantId,
        string? requestedByUserId,
        string? reason,
        CancellationToken ct = default);

    Task MarkExportCompletedAsync(Guid tenantId, CancellationToken ct = default);

    Task<bool> HasPendingRequestAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class TenantDataDeletionRequestService : ITenantDataDeletionRequestService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<TenantDataDeletionRequestService> _logger;

    public TenantDataDeletionRequestService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<TenantDataDeletionRequestService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<bool> HasPendingRequestAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.TenantDataDeletionRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                r => r.TenantId == tenantId
                     && (r.Status == TenantDataDeletionRequestStatuses.Pending
                         || r.Status == TenantDataDeletionRequestStatuses.ExportReady
                         || r.Status == TenantDataDeletionRequestStatuses.Confirmed),
                ct)
            .ConfigureAwait(false);
    }

    public async Task<TenantDataDeletionRequestDto> RequestDeletionAsync(
        Guid tenantId,
        string? requestedByUserId,
        string? reason,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (tenant.CustomerDataPurgedAtUtc.HasValue)
            throw new InvalidOperationException("Customer data was already purged for this tenant.");

        var existing = await db.TenantDataDeletionRequests
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && (r.Status == TenantDataDeletionRequestStatuses.Pending
                            || r.Status == TenantDataDeletionRequestStatuses.ExportReady))
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            return Map(existing);
        }

        var row = new TenantDataDeletionRequest
        {
            TenantId = tenantId,
            Status = TenantDataDeletionRequestStatuses.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = DateTime.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        db.TenantDataDeletionRequests.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant data deletion requested. TenantId={TenantId}, RequestId={RequestId}",
            tenantId,
            row.Id);

        return Map(row);
    }

    public async Task MarkExportCompletedAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var pending = await db.TenantDataDeletionRequests
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && (r.Status == TenantDataDeletionRequestStatuses.Pending
                            || r.Status == TenantDataDeletionRequestStatuses.ExportReady))
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (pending == null)
            return;

        pending.ExportCompletedAtUtc = DateTime.UtcNow;
        pending.Status = TenantDataDeletionRequestStatuses.ExportReady;
        pending.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static TenantDataDeletionRequestDto Map(TenantDataDeletionRequest row) =>
        DataDeletionService.Map(row);
}
