using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly ITagesabschlussReportEnricher _reportEnricher;

    public PosDailyClosingService(
        AppDbContext context,
        ITagesabschlussService tagesabschluss,
        IPosShiftService shiftService,
        ICashRegisterShiftService cashRegisterShift,
        IDailyClosingService dailyClosingSummary,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        ILogger<PosDailyClosingService> logger,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment,
        ITagesabschlussReportEnricher reportEnricher)
    {
        _context = context;
        _tagesabschluss = tagesabschluss;
        _shiftService = shiftService;
        _cashRegisterShift = cashRegisterShift;
        _dailyClosingSummary = dailyClosingSummary;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
        _reportEnricher = reportEnricher;
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
                BlockReason = PosDailyClosingBlockReasons.NoActiveShift,
                Message = "No active shift",
            };
        }

        var register = await _context.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == shift.CashRegisterId, cancellationToken);

        var lastClosingDate = await _tagesabschluss.GetLastClosingDateAsync(shift.CashRegisterId);
        var lastClosingPerformedAt =
            await _tagesabschluss.GetLastClosingPerformedAtForTypeAsync(shift.CashRegisterId, "Daily");
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var paymentsWithoutInvoiceCount = await _tagesabschluss.GetPaymentsWithoutInvoiceCountAsync(
            shift.CashRegisterId,
            dayStartUtc,
            dayEndExclusiveUtc);

        var closedToday = lastClosingDate.HasValue
                          && PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(lastClosingDate.Value)
                          >= viennaToday;

        string? blockReason = null;
        var canClose = true;
        string message;

        if (register == null || register.Status == RegisterStatus.Decommissioned)
        {
            canClose = false;
            blockReason = PosDailyClosingBlockReasons.RegisterUnavailable;
            message = "Cash register is not available for daily closing";
        }
        else if (closedToday)
        {
            canClose = false;
            blockReason = PosDailyClosingBlockReasons.AlreadyClosedToday;
            message = "Daily closing already performed for today";
        }
        else if (paymentsWithoutInvoiceCount > 0)
        {
            canClose = false;
            blockReason = PosDailyClosingBlockReasons.PaymentsWithoutInvoice;
            message =
                $"{paymentsWithoutInvoiceCount} payment(s) without invoice; resolve before closing.";
        }
        else
        {
            message = "Daily closing can be performed";
        }

        return new PosDailyClosingStatusDto
        {
            CanClose = canClose,
            HasActiveShift = true,
            BlockReason = blockReason,
            Message = message,
            LastClosingDate = lastClosingDate,
            LastClosingPerformedAt = lastClosingPerformedAt,
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

        var closingRow = await _context.DailyClosings
            .FirstAsync(c => c.Id == fiscal.ClosingId, cancellationToken);
        closingRow.CashierName = shift.CashierName;
        closingRow.ShiftNumber = await DailyClosingOperationalResolver.ResolveShiftSequenceNumberAsync(
            _context,
            shift.Id,
            cancellationToken);

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

        var previousClosingSignature = await _context.DailyClosings.AsNoTracking()
            .Where(c =>
                c.CashRegisterId == shift.CashRegisterId
                && c.ClosingType == "Daily"
                && c.Status == "Completed"
                && c.ClosingDate < PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(businessDate))
            .OrderByDescending(c => c.ClosingDate)
            .Select(c => c.TseSignature)
            .FirstOrDefaultAsync(cancellationToken);

        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        var closingEntity = await _context.DailyClosings.AsNoTracking()
            .FirstAsync(c => c.Id == fiscal.ClosingId, cancellationToken);
        var cloudContext = await _reportEnricher.BuildContextAsync(closingEntity, cancellationToken);

        var report = DailyClosingReportComposer.Compose(
            closingEntity,
            registerNumber,
            summary,
            request.CashCount,
            shift.Difference,
            totals.Cash,
            cashierName: shift.CashierName ?? closingEntity.CashierName,
            previousClosingSignature: previousClosingSignature,
            shiftNumber: RksvShiftNumberFormatter.Format(
                closingEntity.ShiftNumber > 0
                    ? closingEntity.ShiftNumber
                    : await DailyClosingOperationalResolver.ResolveShiftSequenceNumberAsync(
                        _context,
                        shift.Id,
                        cancellationToken)),
            fiscalEnvironment: fiscalEnvironment,
            cloudContext: cloudContext);

        var auditPrefix = fiscalEnvironment.IsDemoFiscal ? "[TEST] " : string.Empty;
        await _auditLog.LogSystemOperationAsync(
            "PosDailyClosing",
            "cashier_shift",
            cashierUserId,
            actorRole,
            description: $"{auditPrefix}POS daily closing. Sales: {totals.Sales:F2}, Cash diff: {shift.Difference:F2}",
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
