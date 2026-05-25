using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Data;

/// <summary>
/// Ensures active development tenants have at least one default cash register so FA/POS flows can operate without
/// manual bootstrap on a fresh dev database.
/// </summary>
public static class CashRegisterBootstrapSeed
{
    /// <summary>
    /// Inserts a closed default register for each active, non-deleted tenant that currently has no register rows.
    /// Existing tenant registers are preserved.
    /// </summary>
    public static async Task EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var activeTenants = await context.Tenants
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.IsActive
                && t.Status == TenantStatuses.Active)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        if (activeTenants.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var activeTenantIds = activeTenants.Select(t => t.Id).ToList();

        var tenantIdsWithRegisters = await context.CashRegisters
            .AsNoTracking()
            .Where(r => activeTenantIds.Contains(r.TenantId))
            .Select(r => r.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingTenantIds = activeTenants
            .Where(t => !tenantIdsWithRegisters.Contains(t.Id))
            .Select(t => t.Id)
            .ToHashSet();

        if (missingTenantIds.Count == 0)
        {
            var operational = await context.CashRegisters.AsNoTracking()
                .WhereCountsTowardPosOperationalCardinality()
                .CountAsync(cancellationToken);
            if (operational == 0)
            {
                var snapshot = await context.CashRegisters.AsNoTracking()
                    .OrderBy(r => r.RegisterNumber)
                    .Select(r => new
                    {
                        r.Id,
                        r.TenantId,
                        r.RegisterNumber,
                        StatusCode = (int)r.Status,
                        r.IsActive
                    })
                    .Take(30)
                    .ToListAsync(cancellationToken);
                logger.LogWarning(
                    "cash_registers has rows but none are operational (active Open or Closed). POS GET selectable will report no_registers until fixed. Row sample: {@Snapshot}",
                    snapshot);
            }

            return;
        }

        var newRegisters = activeTenants
            .Where(t => missingTenantIds.Contains(t.Id))
            .Select(t => new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = t.Id,
                RegisterNumber = "KASSE-001",
                Location = "Hauptkasse",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CurrentUserId = null,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        await context.CashRegisters.AddRangeAsync(newRegisters, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bootstrap: inserted {Count} default development cash register(s) for tenants without registers.",
            newRegisters.Count);
    }
}
