using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Auto-closes cash registers that have remained open beyond the configured duration.
/// </summary>
public interface IShiftAutoCloseService
{
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

        var openRegisters = await _context.CashRegisters
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

        if (stale.Count == 0)
            return 0;

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
                    $"Auto-close: register open > {maxHours}h",
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
                    shift.EndedAt = endedAt;
                    shift.Status = CashierShiftStatuses.Completed;
                    shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
                        ? $"Auto-close after {maxHours}h"
                        : $"{shift.Notes}; Auto-close after {maxHours}h";
                    shift.UpdatedAt = endedAt;
                    shift.UpdatedBy = "system";
                }

                if (activeShifts.Count > 0)
                    await _context.SaveChangesAsync(cancellationToken);

                closedCount++;
                _logger.LogWarning(
                    "Auto-closed stale register {RegisterId} ({RegisterNumber}) for tenant {TenantId}",
                    register.Id,
                    register.RegisterNumber,
                    register.TenantId);
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

        return closedCount;
    }
}
