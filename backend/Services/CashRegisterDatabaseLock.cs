using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Shared row-level lock for <c>cash_registers</c> lifecycle mutations (open, close, payment commit authorization).
/// PostgreSQL: <c>SELECT … FOR UPDATE</c> inside the caller&apos;s transaction. Other providers: no-op (re-validation still runs in the same transaction scope).
/// </summary>
public static class CashRegisterDatabaseLock
{
    /// <summary>
    /// Blocks until the row lock is acquired. Must be called after <see cref="DatabaseFacade.BeginTransactionAsync"/> on the same <see cref="AppDbContext"/>.
    /// </summary>
    public static async Task AcquireRegisterRowExclusiveLockAsync(
        AppDbContext context,
        Guid registerId,
        CancellationToken cancellationToken = default)
    {
        var provider = context.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT 1 FROM cash_registers WHERE id = {registerId} FOR UPDATE""",
                cancellationToken);
        }
    }
}
