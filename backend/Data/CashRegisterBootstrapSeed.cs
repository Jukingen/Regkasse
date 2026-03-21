using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Data;

/// <summary>
/// Ensures a fresh dev database can run the POS picker: <see cref="CashRegisterPosOperationalCardinality"/> requires at least one
/// active Open or Closed row. Inserts only when <c>cash_registers</c> has <strong>no rows at all</strong> (avoids clashing with existing data).
/// </summary>
public static class CashRegisterBootstrapSeed
{
    /// <summary>
    /// When the table already has rows but none count as operational, logs a warning (admin must fix status / IsActive).
    /// When the table is empty, inserts one open register with no shift owner so POS selectable can return it.
    /// </summary>
    public static async Task EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var anyRow = await context.CashRegisters.AsNoTracking().AnyAsync(cancellationToken);
        if (anyRow)
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

        var now = DateTime.UtcNow;
        await context.CashRegisters.AddAsync(new CashRegister
        {
            Id = Guid.NewGuid(),
            RegisterNumber = "K01",
            Location = "Default",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CurrentUserId = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Bootstrap: inserted default POS cash register {RegisterNumber} into empty cash_registers (Development bootstrap only).",
            "K01");
    }
}
