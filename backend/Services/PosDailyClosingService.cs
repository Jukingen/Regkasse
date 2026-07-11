using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PosDailyClosingService : IPosDailyClosingService
{
    private const decimal DiscrepancyThresholdEur = 5m;

    private readonly AppDbContext _context;
    private readonly ITagesabschlussService _tagesabschluss;
    private readonly IPosShiftService _shiftService;
    private readonly ICashRegisterShiftService _cashRegisterShift;
    private readonly IDailyClosingService _dailyClosingSummary;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<PosDailyClosingService> _logger;

    public PosDailyClosingService(
        AppDbContext context,
        ITagesabschlussService tagesabschluss,
        IPosShiftService shiftService,
        ICashRegisterShiftService cashRegisterShift,
        IDailyClosingService dailyClosingSummary,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        ILogger<PosDailyClosingService> logger)
    {
        _context = context;
        _tagesabschluss = tagesabschluss;
        _shiftService = shiftService;
        _cashRegisterShift = cashRegisterShift;
        _dailyClosingSummary = dailyClosingSummary;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<PosDailyClosingStatusDto> GetStatusAsync(
        string cashierUserId,
        CancellationToken cancellationToken = default)
    {
        var shift = await _context.CashierShifts.AsNoTracking()
            .Where(s => s.CashierId == cashierUserId && s.Status == CashierShiftStatuses.Active && s.IsActive)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (shift == null)
        {
            return new PosDailyClosingStatusDto
            {
                CanClose = false,
                HasActiveShift = false,
                Message = "No active shift",
            };
        }

        var canClose = await _tagesabschluss.CanPerformClosingAsync(shift.CashRegisterId);
        var lastClosingDate = await _tagesabschluss.GetLastClosingDateAsync(shift.CashRegisterId);
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var paymentsWithoutInvoiceCount = await _tagesabschluss.GetPaymentsWithoutInvoiceCountAsync(
            shift.CashRegisterId,
            dayStartUtc,
            dayEndExclusiveUtc);

        var message = canClose
            ? "Daily closing can be performed"
            : paymentsWithoutInvoiceCount > 0
                ? $"{paymentsWithoutInvoiceCount} payment(s) without invoice; resolve before closing."
                : "Daily closing already performed for today";

        return new PosDailyClosingStatusDto
        {
            CanClose = canClose,
            HasActiveShift = true,
            Message = message,
            LastClosingDate = lastClosingDate,
            PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount,
        };
    }

    public async Task<PosDailyClosingResult> PerformDailyClosingAsync(
        string cashierUserId,
        string actorRole,
        PosDailyClosingRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _context.CashierShifts
            .FirstOrDefaultAsync(
                s => s.CashierId == cashierUserId && s.Status == CashierShiftStatuses.Active && s.IsActive,
                cancellationToken);

        if (shift == null)
            throw new PosDailyClosingException(PosDailyClosingFailureKind.NoActiveShift, "No active shift");

        if (!await _tagesabschluss.CanPerformClosingAsync(shift.CashRegisterId))
        {
            var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            var (dayStartUtc, dayEndExclusiveUtc) =
                PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
            var paymentsWithoutInvoiceCount = await _tagesabschluss.GetPaymentsWithoutInvoiceCountAsync(
                shift.CashRegisterId,
                dayStartUtc,
                dayEndExclusiveUtc);

            if (paymentsWithoutInvoiceCount > 0)
            {
                throw new PosDailyClosingException(
                    PosDailyClosingFailureKind.FiscalBlocked,
                    $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice.",
                    paymentsWithoutInvoiceCount);
            }

            throw new PosDailyClosingException(
                PosDailyClosingFailureKind.AlreadyClosed,
                "Daily closing already performed for today");
        }

        var endedAt = DateTime.UtcNow;
        var totals = await _shiftService.GetShiftTotalsAsync(
            shift.CashRegisterId,
            shift.StartedAt,
            endedAt,
            cancellationToken);

        shift.TotalSales = totals.Sales;
        shift.TotalCash = totals.Cash;
        shift.TotalCard = totals.Card;
        shift.CashCount = request.CashCount;
        shift.Difference = request.CashCount - totals.Cash;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            shift.Notes = request.Notes.Trim();

        var fiscal = await _tagesabschluss.PerformDailyClosingAsync(cashierUserId, shift.CashRegisterId);
        if (!fiscal.Success)
        {
            return new PosDailyClosingResult
            {
                Success = false,
                ErrorMessage = fiscal.ErrorMessage ?? "Daily closing failed",
                PaymentsWithoutInvoiceCount = fiscal.PaymentsWithoutInvoiceCount,
            };
        }

        shift.DailyClosingId = fiscal.ClosingId;
        shift.EndedAt = endedAt;
        shift.EndBalance = request.CashCount;
        shift.UpdatedAt = endedAt;
        shift.UpdatedBy = cashierUserId;
        shift.Status = Math.Abs(shift.Difference) > DiscrepancyThresholdEur
            ? CashierShiftStatuses.Discrepancy
            : CashierShiftStatuses.Completed;

        var closeResult = await _cashRegisterShift.TryCloseCashRegisterAsync(
            shift.CashRegisterId,
            cashierUserId,
            request.CashCount,
            cancellationToken,
            completeActiveShifts: false);

        switch (closeResult.Kind)
        {
            case CashRegisterCloseKind.Success:
            case CashRegisterCloseKind.FailedAlreadyClosed:
                break;
            case CashRegisterCloseKind.FailedForbidden:
                throw new PosDailyClosingException(
                    PosDailyClosingFailureKind.RegisterCloseForbidden,
                    "You are not allowed to close this cash register");
            default:
                throw new PosDailyClosingException(
                    PosDailyClosingFailureKind.RegisterCloseFailed,
                    "Cash register could not be closed after daily closing");
        }

        await _context.SaveChangesAsync(cancellationToken);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var businessDate = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var summary = await _dailyClosingSummary.GenerateClosingSummaryAsync(
            tenantId,
            shift.CashRegisterId,
            businessDate,
            cancellationToken);

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == shift.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var report = new PosDailyClosingReportDto
        {
            BusinessDate = businessDate,
            RegisterNumber = registerNumber,
            TotalSales = totals.Sales,
            TotalCash = totals.Cash,
            TotalCard = totals.Card,
            CashCount = request.CashCount,
            Difference = shift.Difference,
            FiscalTotalAmount = fiscal.TotalAmount,
            FiscalTotalTaxAmount = fiscal.TotalTaxAmount,
            FiscalTransactionCount = fiscal.TransactionCount,
            TseSignature = fiscal.TseSignature,
            SnapshotDisclaimerDe = summary.SnapshotDisclaimerDe,
        };

        await _auditLog.LogSystemOperationAsync(
            "PosDailyClosing",
            "cashier_shift",
            cashierUserId,
            actorRole,
            description: $"POS daily closing. Sales: {totals.Sales:F2}, Cash diff: {shift.Difference:F2}",
            notes: request.Notes,
            actionType: AuditEventType.Other,
            entityId: shift.Id,
            tenantId: shift.TenantId);

        _logger.LogInformation(
            "POS daily closing completed for shift {ShiftId} register {RegisterId} by {UserId}",
            shift.Id,
            shift.CashRegisterId,
            cashierUserId);

        return new PosDailyClosingResult
        {
            Success = true,
            Shift = MapShift(shift),
            DailyClosingId = fiscal.ClosingId,
            Report = report,
        };
    }

    private static CashierShiftDto MapShift(CashierShift shift) => new()
    {
        Id = shift.Id,
        TenantId = shift.TenantId,
        CashRegisterId = shift.CashRegisterId,
        CashierId = shift.CashierId,
        CashierName = shift.CashierName,
        StartBalance = shift.StartBalance,
        EndBalance = shift.EndBalance,
        TotalSales = shift.TotalSales,
        TotalCash = shift.TotalCash,
        TotalCard = shift.TotalCard,
        Difference = shift.Difference,
        StartedAt = shift.StartedAt,
        EndedAt = shift.EndedAt,
        Status = shift.Status,
        Notes = shift.Notes,
        DailyClosingId = shift.DailyClosingId,
        CashCount = shift.CashCount,
    };
}
