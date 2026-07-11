using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class AdminShiftOverviewService : IAdminShiftOverviewService
{
    private const int DefaultHistoryLimit = 200;
    private const int MaxHistoryLimit = 500;

    private readonly AppDbContext _context;

    public AdminShiftOverviewService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminShiftOverviewDto> GetOverviewAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int historyLimit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(historyLimit, 1, MaxHistoryLimit);

        IQueryable<CashierShift> baseQuery = _context.CashierShifts.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive);

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            baseQuery = baseQuery.Where(s => s.CashRegisterId == cashRegisterId.Value);

        var registerNumbers = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => new { r.Id, r.RegisterNumber })
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        var activeRows = await baseQuery
            .Where(s => s.Status == CashierShiftStatuses.Active)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        var activeRegisterIds = activeRows.Select(s => s.CashRegisterId).ToHashSet();
        var orphanedOpenRegisters = await _context.CashRegisters.AsNoTracking()
            .Include(r => r.CurrentUser)
            .Where(r => r.TenantId == tenantId
                        && r.Status == RegisterStatus.Open
                        && r.IsActive
                        && (!cashRegisterId.HasValue || r.Id == cashRegisterId.Value))
            .Where(r => !activeRegisterIds.Contains(r.Id))
            .OrderBy(r => r.RegisterNumber)
            .ToListAsync(cancellationToken);

        var orphanedOpenTimes = orphanedOpenRegisters.Count == 0
            ? new Dictionary<Guid, DateTime>()
            : await LoadLastOpenTimesAsync(
                orphanedOpenRegisters.Select(r => r.Id).ToList(),
                cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var activeDtos = activeRows
            .Select(s => MapShift(s, registerNumbers, isOrphaned: false, openDurationHours: ComputeOpenDurationHours(s.StartedAt, nowUtc)))
            .Concat(orphanedOpenRegisters.Select(r => MapOrphanedRegister(r, orphanedOpenTimes, nowUtc)))
            .OrderByDescending(r => r.StartedAt)
            .ToList();

        var historyQuery = baseQuery.Where(s => s.Status != CashierShiftStatuses.Active);
        if (fromUtc.HasValue)
            historyQuery = historyQuery.Where(s => s.StartedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            historyQuery = historyQuery.Where(s => s.StartedAt < toUtc.Value);

        var historyRows = await historyQuery
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var closingShiftRows = await baseQuery
            .Where(s => s.DailyClosingId != null)
            .OrderByDescending(s => s.EndedAt ?? s.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var closingIds = closingShiftRows
            .Select(s => s.DailyClosingId!.Value)
            .Distinct()
            .ToList();

        var fiscalRows = closingIds.Count == 0
            ? new Dictionary<Guid, DailyClosing>()
            : await _context.DailyClosings.AsNoTracking()
                .Where(c => closingIds.Contains(c.Id) && c.TenantId == tenantId)
                .ToDictionaryAsync(c => c.Id, cancellationToken);

        return new AdminShiftOverviewDto
        {
            ActiveShifts = activeDtos,
            ShiftHistory = historyRows.Select(s => MapShift(s, registerNumbers, isOrphaned: false, openDurationHours: null)).ToList(),
            DailyClosings = closingShiftRows
                .Select(s => MapClosing(s, fiscalRows, registerNumbers))
                .ToList(),
        };
    }

    private static AdminShiftRowDto MapShift(
        CashierShift shift,
        IReadOnlyDictionary<Guid, string> registerNumbers,
        bool isOrphaned,
        double? openDurationHours) => new()
    {
        Id = shift.Id,
        CashRegisterId = shift.CashRegisterId,
        RegisterNumber = registerNumbers.GetValueOrDefault(shift.CashRegisterId),
        CashierId = shift.CashierId,
        CashierName = shift.CashierName,
        StartedAt = shift.StartedAt,
        EndedAt = shift.EndedAt,
        StartBalance = shift.StartBalance,
        EndBalance = shift.EndBalance,
        TotalSales = shift.TotalSales,
        TotalCash = shift.TotalCash,
        TotalCard = shift.TotalCard,
        Difference = shift.Difference,
        Status = shift.Status,
        DailyClosingId = shift.DailyClosingId,
        CashCount = shift.CashCount,
        Notes = shift.Notes,
        IsOrphanedRegisterSession = isOrphaned,
        OpenDurationHours = openDurationHours,
    };

    private static AdminShiftRowDto MapOrphanedRegister(
        CashRegister register,
        IReadOnlyDictionary<Guid, DateTime> openTimes,
        DateTime nowUtc)
    {
        openTimes.TryGetValue(register.Id, out var openedAt);
        if (openedAt == default)
            openedAt = register.UpdatedAt ?? nowUtc;

        var owner = register.CurrentUser;
        var ownerName = owner == null
            ? "Unknown"
            : $"{owner.FirstName} {owner.LastName}".Trim() is { Length: > 0 } fullName
                ? fullName
                : owner.UserName ?? owner.Email ?? "Unknown";

        return new AdminShiftRowDto
        {
            Id = register.Id,
            CashRegisterId = register.Id,
            RegisterNumber = register.RegisterNumber,
            CashierId = register.CurrentUserId ?? string.Empty,
            CashierName = ownerName,
            StartedAt = openedAt,
            StartBalance = register.CurrentBalance,
            Status = "RegisterOpen",
            IsOrphanedRegisterSession = true,
            OpenDurationHours = ComputeOpenDurationHours(openedAt, nowUtc),
            Notes = "Register open without active cashier shift row",
        };
    }

    private async Task<Dictionary<Guid, DateTime>> LoadLastOpenTimesAsync(
        IReadOnlyList<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var rows = await _context.CashRegisterTransactions.AsNoTracking()
            .Where(t => registerIds.Contains(t.CashRegisterId)
                        && t.TransactionType == TransactionType.Open
                        && t.IsActive)
            .GroupBy(t => t.CashRegisterId)
            .Select(g => new { RegisterId = g.Key, OpenedAt = g.Max(t => t.TransactionDate) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.RegisterId, r => r.OpenedAt);
    }

    private static double? ComputeOpenDurationHours(DateTime openedAtUtc, DateTime nowUtc)
    {
        var duration = nowUtc - openedAtUtc;
        if (duration.TotalMinutes < 1)
            return 0;
        return Math.Round(duration.TotalHours, 1);
    }
    private static AdminDailyClosingOverviewRowDto MapClosing(
        CashierShift shift,
        IReadOnlyDictionary<Guid, DailyClosing> fiscalRows,
        IReadOnlyDictionary<Guid, string> registerNumbers)
    {
        fiscalRows.TryGetValue(shift.DailyClosingId!.Value, out var fiscal);

        return new AdminDailyClosingOverviewRowDto
        {
            DailyClosingId = shift.DailyClosingId!.Value,
            ShiftId = shift.Id,
            CashRegisterId = shift.CashRegisterId,
            RegisterNumber = registerNumbers.GetValueOrDefault(shift.CashRegisterId),
            CashierName = shift.CashierName,
            ClosingDate = fiscal?.ClosingDate ?? (shift.EndedAt ?? shift.StartedAt),
            ShiftEndedAt = shift.EndedAt,
            TotalSales = shift.TotalSales,
            TotalCash = shift.TotalCash,
            TotalCard = shift.TotalCard,
            CashCount = shift.CashCount,
            Difference = shift.Difference,
            FiscalTotalAmount = fiscal?.TotalAmount ?? 0m,
            FiscalTotalTaxAmount = fiscal?.TotalTaxAmount ?? 0m,
            FiscalTransactionCount = fiscal?.TransactionCount ?? 0,
            HasTseSignature = !string.IsNullOrWhiteSpace(fiscal?.TseSignature),
            ShiftStatus = shift.Status,
            FiscalStatus = fiscal?.Status ?? string.Empty,
        };
    }
}
