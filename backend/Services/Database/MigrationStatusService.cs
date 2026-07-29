using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Database;

public sealed class MigrationStatusService : IMigrationStatusService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<MigrationStatusService> _logger;

    public MigrationStatusService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<MigrationStatusService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<MigrationStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await BuildStatusAsync(db, includeAppliedDetails: false, recentTake: 0, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdminMigrationStatusDto> GetAdminStatusAsync(
        int recentTake = 50,
        CancellationToken cancellationToken = default)
    {
        recentTake = Math.Clamp(recentTake, 1, 200);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var status = await BuildStatusAsync(db, includeAppliedDetails: true, recentTake, cancellationToken)
            .ConfigureAwait(false);

        return new AdminMigrationStatusDto
        {
            Status = status.Status,
            AppliedCount = status.AppliedCount,
            PendingCount = status.PendingCount,
            LatestApplied = status.LatestApplied,
            Pending = status.Pending,
            RecentApplied = status.Applied,
            CheckedAtUtc = status.CheckedAtUtc,
        };
    }

    private async Task<MigrationStatusDto> BuildStatusAsync(
        AppDbContext db,
        bool includeAppliedDetails,
        int recentTake,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTime.UtcNow;
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
            {
                return new MigrationStatusDto
                {
                    Status = "Unhealthy",
                    CheckedAtUtc = checkedAt,
                };
            }

            var appliedIds = (await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false))
                .ToList();
            var pendingIds = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false))
                .ToList();

            IReadOnlyList<MigrationEntryDto> appliedDetails = Array.Empty<MigrationEntryDto>();
            if (includeAppliedDetails)
            {
                appliedDetails = appliedIds
                    .AsEnumerable()
                    .Reverse()
                    .Take(recentTake)
                    .Select(id => new MigrationEntryDto { Id = id })
                    .ToList();
            }

            var status = pendingIds.Count == 0 ? "Healthy" : "Degraded";

            return new MigrationStatusDto
            {
                Status = status,
                AppliedCount = appliedIds.Count,
                PendingCount = pendingIds.Count,
                LatestApplied = appliedIds.Count > 0 ? appliedIds[^1] : null,
                Pending = pendingIds,
                Applied = appliedDetails,
                CheckedAtUtc = checkedAt,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read EF migration status");
            return new MigrationStatusDto
            {
                Status = "Unhealthy",
                CheckedAtUtc = checkedAt,
            };
        }
    }
}
