using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PosShiftService : IPosShiftService
{
  private const decimal DiscrepancyThresholdEur = 5m;

  private readonly AppDbContext _context;
  private readonly ICashRegisterShiftService _cashRegisterShift;
  private readonly ISettingsTenantResolver _tenantResolver;
  private readonly IAuditLogService _auditLog;
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly ILogger<PosShiftService> _logger;

  public PosShiftService(
      AppDbContext context,
      ICashRegisterShiftService cashRegisterShift,
      ISettingsTenantResolver tenantResolver,
      IAuditLogService auditLog,
      UserManager<ApplicationUser> userManager,
      ILogger<PosShiftService> logger)
  {
    _context = context;
    _cashRegisterShift = cashRegisterShift;
    _tenantResolver = tenantResolver;
    _auditLog = auditLog;
    _userManager = userManager;
    _logger = logger;
  }

  public async Task<CurrentShiftResponse> GetCurrentShiftAsync(
      string cashierUserId,
      CancellationToken cancellationToken = default)
  {
    var shift = await _context.CashierShifts.AsNoTracking()
        .Where(s => s.CashierId == cashierUserId && s.Status == CashierShiftStatuses.Active && s.IsActive)
        .OrderByDescending(s => s.StartedAt)
        .FirstOrDefaultAsync(cancellationToken);

    if (shift == null)
      return new CurrentShiftResponse { HasActiveShift = false };

    var liveTotals = await GetShiftTotalsAsync(
        shift.CashRegisterId,
        shift.StartedAt,
        DateTime.UtcNow,
        cancellationToken);

    return new CurrentShiftResponse
    {
      HasActiveShift = true,
      Shift = MapToDto(shift, liveTotals),
    };
  }

  public async Task<CashierShiftDto> StartShiftAsync(
      string cashierUserId,
      string cashierDisplayName,
      StartShiftRequest request,
      CancellationToken cancellationToken = default)
  {
    var hasActive = await _context.CashierShifts.AsNoTracking()
        .AnyAsync(
            s => s.CashierId == cashierUserId && s.Status == CashierShiftStatuses.Active && s.IsActive,
            cancellationToken);

    if (hasActive)
      throw new PosShiftStartException(PosShiftStartResultKind.AlreadyActive, "Already have an active shift");

    var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
    var registerExists = await _context.CashRegisters.AsNoTracking()
        .AnyAsync(r => r.Id == request.CashRegisterId && r.TenantId == tenantId, cancellationToken);

    if (!registerExists)
      throw new PosShiftStartException(PosShiftStartResultKind.RegisterNotFound, "Cash register not found");

    var openResult = await _cashRegisterShift.TryOpenCashRegisterAsync(
        request.CashRegisterId,
        cashierUserId,
        request.StartBalance,
        "POS shift start",
        allowIdempotentSameUser: true,
        cancellationToken);

    switch (openResult.Kind)
    {
      case CashRegisterOpenKind.SuccessOpened:
      case CashRegisterOpenKind.SuccessIdempotentAlreadyOpen:
        break;
      case CashRegisterOpenKind.FailedNotFound:
        throw new PosShiftStartException(PosShiftStartResultKind.RegisterNotFound, "Cash register not found");
      case CashRegisterOpenKind.FailedConflictOtherUser:
        throw new PosShiftStartException(
            PosShiftStartResultKind.RegisterOpenConflict,
            "Cash register is held by another user");
      default:
        throw new PosShiftStartException(
            PosShiftStartResultKind.RegisterOpenFailed,
            "Cash register could not be opened for this shift");
    }

    var displayName = await ResolveCashierDisplayNameAsync(cashierUserId, cashierDisplayName, cancellationToken);
    var startedAt = DateTime.UtcNow;
    var shift = new CashierShift
    {
      TenantId = tenantId,
      CashRegisterId = request.CashRegisterId,
      CashierId = cashierUserId,
      CashierName = displayName,
      StartBalance = request.StartBalance,
      StartedAt = startedAt,
      Status = CashierShiftStatuses.Active,
      CreatedAt = startedAt,
      UpdatedAt = startedAt,
      CreatedBy = cashierUserId,
    };

    _context.CashierShifts.Add(shift);
    await _context.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
        "Cashier shift {ShiftId} started by {UserId} on register {RegisterId}",
        shift.Id,
        cashierUserId,
        request.CashRegisterId);

    return MapToDto(shift);
  }

  public async Task<EndShiftResponse> EndShiftAsync(
      string cashierUserId,
      string actorRole,
      EndShiftRequest request,
      CancellationToken cancellationToken = default)
  {
    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    try
    {
      var shift = await _context.CashierShifts
          .FirstOrDefaultAsync(
              s => s.CashierId == cashierUserId && s.Status == CashierShiftStatuses.Active && s.IsActive,
              cancellationToken);

      if (shift == null)
        throw new PosShiftEndException(PosShiftEndResultKind.NoActiveShift, "No active shift found");

      var endedAt = DateTime.UtcNow;
      var totals = await GetShiftTotalsAsync(shift.CashRegisterId, shift.StartedAt, endedAt, cancellationToken);

      shift.EndBalance = request.EndBalance;
      shift.TotalSales = totals.Sales;
      shift.TotalCash = totals.Cash;
      shift.TotalCard = totals.Card;
      shift.Difference = request.EndBalance - shift.StartBalance - totals.Cash;
      shift.EndedAt = endedAt;
      shift.Status = Math.Abs(shift.Difference) > DiscrepancyThresholdEur
          ? CashierShiftStatuses.Discrepancy
          : CashierShiftStatuses.Completed;
      shift.Notes = request.Notes;
      shift.UpdatedAt = endedAt;
      shift.UpdatedBy = cashierUserId;

      var closeResult = await _cashRegisterShift.TryCloseCashRegisterAsync(
          shift.CashRegisterId,
          cashierUserId,
          request.EndBalance,
          cancellationToken,
          completeActiveShifts: false);

      switch (closeResult.Kind)
      {
        case CashRegisterCloseKind.Success:
        case CashRegisterCloseKind.FailedAlreadyClosed:
          break;
        case CashRegisterCloseKind.FailedForbidden:
          throw new PosShiftEndException(
              PosShiftEndResultKind.RegisterCloseForbidden,
              "You are not allowed to close this cash register");
        default:
          throw new PosShiftEndException(
              PosShiftEndResultKind.RegisterCloseFailed,
              "Cash register could not be closed");
      }

      await _context.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      var registerNumber = await _context.CashRegisters.AsNoTracking()
          .Where(r => r.Id == shift.CashRegisterId)
          .Select(r => r.RegisterNumber)
          .FirstOrDefaultAsync(cancellationToken);

      var receipt = BuildClosingReceipt(shift, registerNumber);

      await _auditLog.LogSystemOperationAsync(
          "ShiftEnded",
          "cashier_shift",
          cashierUserId,
          actorRole,
          description: $"Shift ended. Sales: {totals.Sales:F2}, Difference: {shift.Difference:F2}",
          notes: request.Notes,
          actionType: AuditEventType.Other,
          entityId: shift.Id,
          tenantId: shift.TenantId);

      _logger.LogInformation(
          "Cashier shift {ShiftId} ended by {UserId}. Status={Status}, Difference={Difference}",
          shift.Id,
          cashierUserId,
          shift.Status,
          shift.Difference);

      return new EndShiftResponse
      {
        Shift = MapToDto(shift),
        Receipt = receipt,
      };
    }
    catch
    {
      await transaction.RollbackAsync(cancellationToken);
      throw;
    }
  }

  public async Task<ShiftTotalsDto> GetShiftTotalsAsync(
      Guid cashRegisterId,
      DateTime startedAtUtc,
      DateTime endedAtUtc,
      CancellationToken cancellationToken = default)
  {
    var payments = await _context.PaymentDetails.AsNoTracking()
        .Where(p => p.CashRegisterId == cashRegisterId
                    && p.CreatedAt >= startedAtUtc
                    && p.CreatedAt <= endedAtUtc
                    && p.IsActive
                    && !p.IsRefund
                    && !p.IsStorno
                    && p.RksvSpecialReceiptKind == null)
        .ToListAsync(cancellationToken);

    static decimal SumForMethod(IEnumerable<PaymentDetails> rows, PaymentMethod method)
    {
      var raw = ((int)method).ToString();
      return rows.Where(p => p.PaymentMethodRaw == raw).Sum(p => p.TotalAmount);
    }

    return new ShiftTotalsDto
    {
      Sales = payments.Sum(p => p.TotalAmount),
      Cash = SumForMethod(payments, PaymentMethod.Cash),
      Card = SumForMethod(payments, PaymentMethod.Card),
    };
  }

  private async Task<string> ResolveCashierDisplayNameAsync(
      string cashierUserId,
      string fallback,
      CancellationToken cancellationToken)
  {
    var user = await _userManager.FindByIdAsync(cashierUserId);
    if (user == null)
      return string.IsNullOrWhiteSpace(fallback) ? "Unknown" : fallback;

    var fullName = $"{user.FirstName} {user.LastName}".Trim();
    if (!string.IsNullOrWhiteSpace(fullName))
      return fullName;

    if (!string.IsNullOrWhiteSpace(user.UserName))
      return user.UserName;

    return string.IsNullOrWhiteSpace(fallback) ? "Unknown" : fallback;
  }

  private static CashierShiftDto MapToDto(CashierShift shift, ShiftTotalsDto? liveTotals = null) => new()
  {
    Id = shift.Id,
    TenantId = shift.TenantId,
    CashRegisterId = shift.CashRegisterId,
    CashierId = shift.CashierId,
    CashierName = shift.CashierName,
    StartBalance = shift.StartBalance,
    EndBalance = shift.EndBalance,
    TotalSales = liveTotals?.Sales ?? shift.TotalSales,
    TotalCash = liveTotals?.Cash ?? shift.TotalCash,
    TotalCard = liveTotals?.Card ?? shift.TotalCard,
    Difference = shift.Difference,
    StartedAt = shift.StartedAt,
    EndedAt = shift.EndedAt,
    Status = shift.Status,
    Notes = shift.Notes,
    DailyClosingId = shift.DailyClosingId,
    CashCount = shift.CashCount,
  };

  private static ShiftClosingReceiptDto BuildClosingReceipt(CashierShift shift, string? registerNumber) => new()
  {
    ShiftId = shift.Id,
    CashierName = shift.CashierName,
    RegisterNumber = registerNumber,
    StartedAt = shift.StartedAt,
    EndedAt = shift.EndedAt ?? DateTime.UtcNow,
    StartBalance = shift.StartBalance,
    EndBalance = shift.EndBalance,
    TotalSales = shift.TotalSales,
    TotalCash = shift.TotalCash,
    TotalCard = shift.TotalCard,
    Difference = shift.Difference,
    Status = shift.Status,
    Notes = shift.Notes,
  };
}
