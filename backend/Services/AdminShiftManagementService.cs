using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class AdminShiftManagementService : IAdminShiftManagementService
{
    private readonly AppDbContext _context;
    private readonly ICashRegisterShiftService _cashRegisterShift;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AdminShiftManagementService> _logger;

    public AdminShiftManagementService(
        AppDbContext context,
        ICashRegisterShiftService cashRegisterShift,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        ILogger<AdminShiftManagementService> logger)
    {
        _context = context;
        _cashRegisterShift = cashRegisterShift;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<AdminShiftForceCloseResult> ForceCloseRegisterAsync(
        Guid cashRegisterId,
        string actorUserId,
        string actorRole,
        decimal? closingBalance,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var register = await _context.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken);

        if (register == null)
            return AdminShiftForceCloseResult.NotFound(cashRegisterId);

        if (register.Status != RegisterStatus.Open)
            return AdminShiftForceCloseResult.AlreadyClosed(cashRegisterId);

        var balance = closingBalance ?? register.CurrentBalance;
        var description = string.IsNullOrWhiteSpace(reason)
            ? "Admin force-close"
            : $"Admin force-close: {reason.Trim()}";

        var closeResult = await _cashRegisterShift.TryForceCloseCashRegisterAsync(
            cashRegisterId,
            actorUserId,
            balance,
            description,
            cancellationToken);

        switch (closeResult.Kind)
        {
            case CashRegisterCloseKind.Success:
                break;
            case CashRegisterCloseKind.FailedNotFound:
                return AdminShiftForceCloseResult.NotFound(cashRegisterId);
            case CashRegisterCloseKind.FailedAlreadyClosed:
                return AdminShiftForceCloseResult.AlreadyClosed(cashRegisterId);
            default:
                throw new InvalidOperationException($"Unexpected force-close result: {closeResult.Kind}");
        }

        var closedShiftCount = await CloseActiveShiftsForRegisterAsync(
            cashRegisterId,
            tenantId,
            actorUserId,
            description,
            cancellationToken);

        await _auditLog.LogSystemOperationAsync(
            "ShiftForceClosed",
            "cash_register",
            actorUserId,
            actorRole,
            description: $"Force-closed register {register.RegisterNumber}. ClosedShiftCount={closedShiftCount}",
            notes: reason,
            actionType: AuditEventType.Other,
            entityId: cashRegisterId,
            tenantId: tenantId);

        _logger.LogWarning(
            "Register {RegisterId} force-closed by {ActorUserId}. ClosedShiftCount={ClosedShiftCount}",
            cashRegisterId,
            actorUserId,
            closedShiftCount);

        return AdminShiftForceCloseResult.Succeeded(cashRegisterId, closedShiftCount);
    }

    private async Task<int> CloseActiveShiftsForRegisterAsync(
        Guid cashRegisterId,
        Guid tenantId,
        string actorUserId,
        string note,
        CancellationToken cancellationToken)
    {
        var activeShifts = await _context.CashierShifts
            .Where(s => s.TenantId == tenantId
                        && s.CashRegisterId == cashRegisterId
                        && s.Status == CashierShiftStatuses.Active
                        && s.IsActive)
            .ToListAsync(cancellationToken);

        if (activeShifts.Count == 0)
            return 0;

        var endedAt = DateTime.UtcNow;
        foreach (var shift in activeShifts)
        {
            shift.EndedAt = endedAt;
            shift.Status = CashierShiftStatuses.Completed;
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
                ? note
                : $"{shift.Notes}; {note}";
            shift.UpdatedAt = endedAt;
            shift.UpdatedBy = actorUserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return activeShifts.Count;
    }
}
