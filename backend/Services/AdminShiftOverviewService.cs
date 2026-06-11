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
            ActiveShifts = activeRows.Select(s => MapShift(s, registerNumbers)).ToList(),
            ShiftHistory = historyRows.Select(s => MapShift(s, registerNumbers)).ToList(),
            DailyClosings = closingShiftRows
                .Select(s => MapClosing(s, fiscalRows, registerNumbers))
                .ToList(),
        };
    }

    private static AdminShiftRowDto MapShift(
        CashierShift shift,
        IReadOnlyDictionary<Guid, string> registerNumbers) => new()
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
    };

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
