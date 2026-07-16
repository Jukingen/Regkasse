using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Auto-closes cash registers / cashier shifts left open beyond the configured inactivity duration.
/// </summary>
public interface IShiftAutoCloseService
{
    /// <summary>
    /// Force-closes stale open registers and soft-closes orphaned active CashierShift rows.
    /// Returns the number of registers closed (orphan shift soft-closes are logged separately).
    /// </summary>
    Task<int> CloseStaleOpenRegistersAsync(CancellationToken cancellationToken = default);
}

public sealed class ShiftAutoCloseService : IShiftAutoCloseService
{
    private readonly AppDbContext _context;
    private readonly ICashRegisterShiftService _cashRegisterShift;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ShiftAutoCloseOptions _options;
    private readonly ILogger<ShiftAutoCloseService> _logger;

    public ShiftAutoCloseService(
        AppDbContext context,
        ICashRegisterShiftService cashRegisterShift,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<ShiftAutoCloseOptions> options,
        ILogger<ShiftAutoCloseService> logger)
    {
        _context = context;
        _cashRegisterShift = cashRegisterShift;
        _tenantAccessor = tenantAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> CloseStaleOpenRegistersAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return 0;

        var maxHours = Math.Max(1, _options.MaxOpenDurationHours);
        var cutoff = DateTime.UtcNow.AddHours(-maxHours);

        // Hosted worker runs with no tenant context; IgnoreQueryFilters is required for the sweep.
        var openRegisters = await _context.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Status == RegisterStatus.Open && r.IsActive)
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                r.RegisterNumber,
                r.CurrentBalance,
                r.UpdatedAt,
                LastOpenAt = _context.CashRegisterTransactions
                    .IgnoreQueryFilters()
                    .Where(t => t.CashRegisterId == r.Id
                                && t.TransactionType == TransactionType.Open
                                && t.IsActive)
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => (DateTime?)t.TransactionDate)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var stale = openRegisters
            .Where(r =>
            {
                var openedAt = r.LastOpenAt ?? r.UpdatedAt;
                return openedAt <= cutoff;
            })
            .ToList();

        var closedCount = 0;
        foreach (var register in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previousTenant = _tenantAccessor.TenantId;
            try
            {
                _tenantAccessor.TenantId = register.TenantId;

                var result = await _cashRegisterShift.TryForceCloseCashRegisterAsync(
                    register.Id,
                    actorUserId: "system",
                    register.CurrentBalance,
                    $"Auto-close: inactivity > {maxHours}h",
                    cancellationToken);

                if (result.Kind != CashRegisterCloseKind.Success)
                    continue;

                var activeShifts = await _context.CashierShifts
                    .Where(s => s.TenantId == register.TenantId
                                && s.CashRegisterId == register.Id
                                && s.Status == CashierShiftStatuses.Active
                                && s.IsActive)
                    .ToListAsync(cancellationToken);

                var endedAt = DateTime.UtcNow;
                foreach (var shift in activeShifts)
                {
                    CompleteShiftForInactivity(shift, endedAt, maxHours);
                }

                if (activeShifts.Count > 0)
                    await _context.SaveChangesAsync(cancellationToken);

                closedCount++;
                _logger.LogWarning(
                    "Auto-closed stale register {RegisterId} ({RegisterNumber}) for tenant {TenantId} after {MaxHours}h inactivity",
                    register.Id,
                    register.RegisterNumber,
                    register.TenantId,
                    maxHours);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    ex,
                    "Auto-close failed for register {RegisterId} ({RegisterNumber})",
                    register.Id,
                    register.RegisterNumber);
            }
            finally
            {
                _tenantAccessor.TenantId = previousTenant;
            }
        }

        var orphanShiftsClosed = await SoftCloseOrphanActiveShiftsAsync(cutoff, maxHours, cancellationToken);
        if (orphanShiftsClosed > 0)
        {
            _logger.LogWarning(
                "Auto-closed {Count} orphaned active CashierShift row(s) after {MaxHours}h inactivity",
                orphanShiftsClosed,
                maxHours);
        }

        return closedCount;
    }

    /// <summary>
    /// Soft-closes Active CashierShift rows older than the cutoff when the register is already closed
    /// (or the shift was left behind). Does not close cash registers.
    /// </summary>
    private async Task<int> SoftCloseOrphanActiveShiftsAsync(
        DateTime cutoffUtc,
        int maxHours,
        CancellationToken cancellationToken)
    {
        var previousTenant = _tenantAccessor.TenantId;
        try
        {
            // Clear tenant so we can load across tenants with IgnoreQueryFilters.
            _tenantAccessor.TenantId = null;

            var orphans = await _context.CashierShifts
                .IgnoreQueryFilters()
                .Where(s => s.Status == CashierShiftStatuses.Active
                            && s.IsActive
                            && s.StartedAt <= cutoffUtc)
                .ToListAsync(cancellationToken);

            if (orphans.Count == 0)
                return 0;

            var endedAt = DateTime.UtcNow;
            foreach (var shift in orphans)
            {
                CompleteShiftForInactivity(shift, endedAt, maxHours);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return orphans.Count;
        }
        finally
        {
            _tenantAccessor.TenantId = previousTenant;
        }
    }

    private static void CompleteShiftForInactivity(CashierShift shift, DateTime endedAtUtc, int maxHours)
    {
        shift.EndedAt = endedAtUtc;
        shift.Status = CashierShiftStatuses.Completed;
        shift.IsAutoClosed = true;
        var note = $"Auto-closed: inactivity > {maxHours}h";
        shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
            ? note
            : $"{shift.Notes}; {note}";
        shift.UpdatedAt = endedAtUtc;
        shift.UpdatedBy = "system";
    }
}
