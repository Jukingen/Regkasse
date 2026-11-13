using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IAutoTagesabschlussService
{
    /// <summary>
    /// Closes yesterday's Vienna business day for registers that still need a Daily closing
    /// after the tenant's configured fallback time. Returns how many closings were created.
    /// </summary>
    Task<int> RunFallbackAsync(DateTime? utcNow = null, CancellationToken cancellationToken = default);
}

public sealed class AutoTagesabschlussService : IAutoTagesabschlussService
{
    public const string SystemActorUserId = "system";

    private readonly AppDbContext _context;
    private readonly ITagesabschlussService _tagesabschluss;
    private readonly ICashRegisterShiftService _cashRegisterShift;
    private readonly IMonatsbelegClosingService _monatsbeleg;
    private readonly IJahresbelegClosingService _jahresbeleg;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IActivityEventService _activity;
    private readonly AutoTagesabschlussOptions _options;
    private readonly ILogger<AutoTagesabschlussService> _logger;

    public AutoTagesabschlussService(
        AppDbContext context,
        ITagesabschlussService tagesabschluss,
        ICashRegisterShiftService cashRegisterShift,
        IMonatsbelegClosingService monatsbeleg,
        IJahresbelegClosingService jahresbeleg,
        ICurrentTenantAccessor tenantAccessor,
        IActivityEventService activity,
        IOptions<AutoTagesabschlussOptions> options,
        ILogger<AutoTagesabschlussService> logger)
    {
        _context = context;
        _tagesabschluss = tagesabschluss;
        _cashRegisterShift = cashRegisterShift;
        _monatsbeleg = monatsbeleg;
        _jahresbeleg = jahresbeleg;
        _tenantAccessor = tenantAccessor;
        _activity = activity;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RunFallbackAsync(DateTime? utcNow = null, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return 0;

        var nowUtc = utcNow ?? DateTime.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
            nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var previousTenant = _tenantAccessor.TenantId;
        try
        {
            _tenantAccessor.TenantId = null;

            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var closed = 0;
            foreach (var tenant in tenants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    closed += await RunTenantFallbackAsync(
                            tenant.Id,
                            tenant.Name,
                            nowUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Automatic Tagesabschluss failed for tenant {TenantId}",
                        tenant.Id);
                }
            }

            if (closed > 0)
            {
                _logger.LogWarning(
                    "Automatic Tagesabschluss created {Count} daily closing(s).",
                    closed);
            }

            return closed;
        }
        finally
        {
            _tenantAccessor.TenantId = previousTenant;
        }
    }

    private async Task<int> RunTenantFallbackAsync(
        Guid tenantId,
        string? tenantName,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
            return 0;

        var latestEligibleDay = AutoTagesabschlussCutoff.GetLatestEligibleBusinessDay(
            utcNow,
            settings.HourVienna,
            settings.MinuteVienna);

        var registers = await _context.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location, r.CurrentUserId, r.CurrentBalance, r.Status, r.LastBalanceUpdate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var closed = 0;
        foreach (var register in registers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var missedDays = await ListMissedInvoiceBusinessDaysAsync(
                    tenantId,
                    register.Id,
                    utcNow,
                    latestEligibleDay,
                    cancellationToken)
                .ConfigureAwait(false);
            var sessionStartedUtc = await TryGetOpenSessionStartUtcAsync(
                    tenantId,
                    register.Id,
                    register.Status,
                    register.LastBalanceUpdate,
                    cancellationToken)
                .ConfigureAwait(false);
            var (todayStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(
                AutoTagesabschlussCutoff.GetTodayBusinessDay(utcNow));
            var (yesterdayStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(
                AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utcNow));
            var pastCutoff = AutoTagesabschlussCutoff.IsPastCutoff(
                utcNow,
                settings.HourVienna,
                settings.MinuteVienna);
            var emptyCloseBeforeUtc = pastCutoff ? todayStartUtc : yesterdayStartUtc;
            var leftOpenFromPreviousDay = sessionStartedUtc is DateTime started && started < todayStartUtc;
            var closeEmptySession = sessionStartedUtc is DateTime emptyStart && emptyStart < emptyCloseBeforeUtc;
            if (leftOpenFromPreviousDay)
            {
                _logger.LogWarning(
                    "Cash register {RegisterId} ({RegisterNumber}) tenant {TenantId} is still open from a previous Vienna day (latest eligible {BusinessDay:yyyy-MM-dd}).",
                    register.Id,
                    register.RegisterNumber,
                    tenantId,
                    latestEligibleDay);
            }

            if (missedDays.Count == 0)
            {
                if (closeEmptySession)
                {
                    var openDay = pastCutoff
                        ? AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utcNow)
                        : AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utcNow).AddDays(-1);
                    var (openStart, openEnd) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(openDay);
                    var openOrders = await CountOpenOrdersAsync(tenantId, openStart, openEnd, cancellationToken)
                        .ConfigureAwait(false);
                    var policy = AutoTagesabschlussOpenOrdersPolicies.Normalize(settings.OpenOrdersPolicy);
                    if (openOrders > 0 && policy == AutoTagesabschlussOpenOrdersPolicies.Block)
                    {
                        await PublishOpenOrdersWarningAsync(
                                tenantId,
                                tenantName,
                                register.Id,
                                register.RegisterNumber,
                                register.Location,
                                latestEligibleDay,
                                openOrders,
                                blocked: true,
                                cancellationToken)
                            .ConfigureAwait(false);
                        _logger.LogWarning(
                            "Automatic Tagesabschluss did not close empty register {RegisterId} tenant {TenantId}: {OpenOrders} open order(s).",
                            register.Id,
                            tenantId,
                            openOrders);
                        continue;
                    }

                    var (_, _, emptyCashNote) = await ReadCashCountAsync(tenantId, register.Id, cancellationToken)
                        .ConfigureAwait(false);
                    await CompleteShiftsAndCloseRegisterAsync(
                            tenantId,
                            register.Id,
                            register.Status,
                            register.CurrentBalance,
                            dailyClosingId: null,
                            cashCount: null,
                            emptyCashNote,
                            cancellationToken)
                        .ConfigureAwait(false);
                    _logger.LogWarning(
                        "Automatic Tagesabschluss closed register {RegisterId} ({RegisterNumber}) tenant {TenantId} with no fiscal invoices on missed days.",
                        register.Id,
                        register.RegisterNumber,
                        tenantId);
                }

                continue;
            }

            var (missedStart, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(missedDays[0]);
            var (_, missedEnd) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(missedDays[^1]);
            var openOrdersForClosing = await CountOpenOrdersAsync(tenantId, missedStart, missedEnd, cancellationToken)
                .ConfigureAwait(false);
            var closingPolicy = AutoTagesabschlussOpenOrdersPolicies.Normalize(settings.OpenOrdersPolicy);
            if (openOrdersForClosing > 0)
            {
                await PublishOpenOrdersWarningAsync(
                        tenantId,
                        tenantName,
                        register.Id,
                        register.RegisterNumber,
                        register.Location,
                        missedDays[0],
                        openOrdersForClosing,
                        blocked: closingPolicy == AutoTagesabschlussOpenOrdersPolicies.Block,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (closingPolicy == AutoTagesabschlussOpenOrdersPolicies.Block)
                {
                    _logger.LogWarning(
                        "Automatic Tagesabschluss blocked for register {RegisterId} tenant {TenantId}: {OpenOrders} open order(s).",
                        register.Id,
                        tenantId,
                        openOrdersForClosing);
                    continue;
                }

                if (closingPolicy == AutoTagesabschlussOpenOrdersPolicies.ForceWithWarning)
                {
                    _logger.LogWarning(
                        "Automatic Tagesabschluss ForceWithWarning for register {RegisterId} tenant {TenantId}: {OpenOrders} open table(s)/cart(s). Unpaid amounts stay out of the Z report.",
                        register.Id,
                        tenantId,
                        openOrdersForClosing);
                }
            }

            var actorUserId = await ResolveActorUserIdAsync(
                    tenantId,
                    register.Id,
                    register.CurrentUserId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                _logger.LogWarning(
                    "Automatic Tagesabschluss skipped for register {RegisterId}: no actor user id.",
                    register.Id);
                continue;
            }

            var (cashCount, cashDifference, cashNote) = await ReadCashCountAsync(
                    tenantId,
                    register.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            var previousTenant = _tenantAccessor.TenantId;
            _tenantAccessor.TenantId = tenantId;
            try
            {
                foreach (var businessDay in missedDays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await _tagesabschluss.PerformDailyClosingAsync(
                            actorUserId,
                            register.Id,
                            new DailyClosingPerformOptions
                            {
                                Trigger = DailyClosingTriggers.Automatic,
                                CashCountNote = cashNote,
                                CashCount = cashCount,
                                CashDifference = cashDifference,
                                OpenOrdersCount = openOrdersForClosing,
                                OpenOrdersForced = openOrdersForClosing > 0
                                    && closingPolicy != AutoTagesabschlussOpenOrdersPolicies.NotifyAndContinue,
                            },
                            businessDay,
                            AutoTagesabschlussSettings.AutomaticLateReason)
                        .ConfigureAwait(false);

                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "Automatic Tagesabschluss did not succeed for register {RegisterId} businessDay={BusinessDay:yyyy-MM-dd}: {Error}",
                            register.Id,
                            businessDay,
                            result.ErrorMessage);
                        break;
                    }

                    await CompleteShiftsAndCloseRegisterAsync(
                            tenantId,
                            register.Id,
                            register.Status,
                            register.CurrentBalance,
                            result.ClosingId,
                            cashCount,
                            cashNote,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await TryPeriodClosingsAsync(
                            actorUserId,
                            register.Id,
                            businessDay,
                            cancellationToken)
                        .ConfigureAwait(false);

                    closed++;
                    _logger.LogWarning(
                        "Automatic Tagesabschluss created closing {ClosingId} for register {RegisterId} ({RegisterNumber}) tenant {TenantId} businessDay={BusinessDay:yyyy-MM-dd}",
                        result.ClosingId,
                        register.Id,
                        register.RegisterNumber,
                        tenantId,
                        businessDay);
                }
            }
            finally
            {
                _tenantAccessor.TenantId = previousTenant;
            }
        }

        return closed;
    }

    private async Task<List<DateTime>> ListMissedInvoiceBusinessDaysAsync(
        Guid tenantId,
        Guid cashRegisterId,
        DateTime utcNow,
        DateTime latestEligibleDay,
        CancellationToken cancellationToken)
    {
        var today = AutoTagesabschlussCutoff.GetTodayBusinessDay(utcNow);
        var (todayStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(today);
        var createdAts = await _context.Invoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.CashRegisterId == cashRegisterId
                && i.CreatedAt < todayStartUtc
                && i.Status == InvoiceStatus.Paid)
            .Select(i => i.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var days = createdAts
            .Select(PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant)
            .Where(day => day <= latestEligibleDay)
            .Distinct()
            .OrderBy(day => day)
            .ToList();

        var missed = new List<DateTime>();
        foreach (var day in days)
        {
            var anchor = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(day);
            var alreadyClosed = await _context.DailyClosings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    d =>
                        d.TenantId == tenantId
                        && d.CashRegisterId == cashRegisterId
                        && d.ClosingType == "Daily"
                        && d.ClosingDate == anchor,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!alreadyClosed)
                missed.Add(day);
        }

        return missed;
    }

    private async Task<DateTime?> TryGetOpenSessionStartUtcAsync(
        Guid tenantId,
        Guid cashRegisterId,
        RegisterStatus status,
        DateTime lastBalanceUpdate,
        CancellationToken cancellationToken)
    {
        if (status != RegisterStatus.Open)
            return null;

        var shiftStarted = await _context.CashierShifts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId
                && s.CashRegisterId == cashRegisterId
                && s.Status == CashierShiftStatuses.Active
                && s.IsActive)
            .OrderBy(s => s.StartedAt)
            .Select(s => (DateTime?)s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (shiftStarted is DateTime started)
            return SpecifyUtc(started);

        var lastOpen = await _context.CashRegisterTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.CashRegisterId == cashRegisterId
                && t.TransactionType == TransactionType.Open
                && t.IsActive)
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => (DateTime?)t.TransactionDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (lastOpen is DateTime opened)
            return SpecifyUtc(opened);

        return SpecifyUtc(lastBalanceUpdate);
    }

    private static DateTime SpecifyUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private async Task<AutoTagesabschlussSettings> LoadSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var row = await _context.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.AutoTagesabschluss)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var settings = row ?? AutoTagesabschlussSettings.CreateDefault();
        if (row == null)
        {
            settings.HourVienna = _options.DefaultHourVienna;
            settings.MinuteVienna = _options.DefaultMinuteVienna;
        }

        settings.Normalize();
        return settings;
    }

    private async Task<int> CountOpenOrdersAsync(
        Guid tenantId,
        DateTime dayStartUtc,
        DateTime dayEndExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var userIds = await _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
            return 0;

        var openTables = await _context.TableOrders
            .AsNoTracking()
            .CountAsync(
                o =>
                    userIds.Contains(o.UserId)
                    && o.IsActive
                    && o.Status != TableOrderStatus.Completed
                    && o.Status != TableOrderStatus.Cancelled
                    && ((o.LastModifiedTime != null
                            && o.LastModifiedTime >= dayStartUtc
                            && o.LastModifiedTime < dayEndExclusiveUtc)
                        || (o.LastModifiedTime == null
                            && o.OrderStartTime >= dayStartUtc
                            && o.OrderStartTime < dayEndExclusiveUtc)),
                cancellationToken)
            .ConfigureAwait(false);

        var openCarts = await _context.Carts
            .AsNoTracking()
            .CountAsync(
                c =>
                    userIds.Contains(c.UserId)
                    && c.IsActive
                    && c.Status == CartStatus.Active
                    && ((c.UpdatedAt != null && c.UpdatedAt >= dayStartUtc && c.UpdatedAt < dayEndExclusiveUtc)
                        || (c.UpdatedAt == null && c.CreatedAt >= dayStartUtc && c.CreatedAt < dayEndExclusiveUtc)),
                cancellationToken)
            .ConfigureAwait(false);

        return openTables + openCarts;
    }

    private async Task<string?> ResolveActorUserIdAsync(
        Guid tenantId,
        Guid cashRegisterId,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(currentUserId))
            return currentUserId;

        var lastCashier = await _context.CashierShifts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CashRegisterId == cashRegisterId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.CashierId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(lastCashier))
            return lastCashier;

        return await _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .OrderByDescending(m => m.IsOwner)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(decimal? CashCount, decimal? Difference, string Note)> ReadCashCountAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var shift = await _context.CashierShifts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId
                && s.CashRegisterId == cashRegisterId
                && s.IsActive)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new { s.CashCount, s.Difference, s.TotalCash })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (shift?.CashCount is decimal counted)
        {
            var difference = counted - shift.TotalCash;
            return (counted, difference, $"Kassensturz: {counted:0.00} (Differenz {difference:0.00})");
        }

        return (null, null, AutoTagesabschlussSettings.NoCashCountNote);
    }

    private async Task CompleteShiftsAndCloseRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        RegisterStatus registerStatus,
        decimal currentBalance,
        Guid? dailyClosingId,
        decimal? cashCount,
        string? cashNote,
        CancellationToken cancellationToken)
    {
        var endedAt = DateTime.UtcNow;
        var activeShifts = await _context.CashierShifts
            .IgnoreQueryFilters()
            .Where(s =>
                s.TenantId == tenantId
                && s.CashRegisterId == cashRegisterId
                && s.Status == CashierShiftStatuses.Active
                && s.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var shift in activeShifts)
        {
            shift.EndedAt = endedAt;
            shift.Status = CashierShiftStatuses.Completed;
            shift.IsAutoClosed = true;
            shift.DailyClosingId ??= dailyClosingId;
            if (cashCount is decimal counted)
                shift.EndBalance = counted;
            var note = string.IsNullOrWhiteSpace(cashNote)
                ? "Automatischer Tagesabschluss"
                : $"Automatischer Tagesabschluss; {cashNote}";
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
                ? note
                : $"{shift.Notes}; {note}";
            shift.UpdatedAt = endedAt;
            shift.UpdatedBy = SystemActorUserId;
        }

        if (activeShifts.Count > 0)
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (registerStatus == RegisterStatus.Open)
        {
            var closeResult = await _cashRegisterShift.TryForceCloseCashRegisterAsync(
                    cashRegisterId,
                    SystemActorUserId,
                    cashCount ?? currentBalance,
                    "Automatic Tagesabschluss fallback",
                    cancellationToken)
                .ConfigureAwait(false);
            if (closeResult.Kind is not CashRegisterCloseKind.Success
                and not CashRegisterCloseKind.FailedAlreadyClosed)
            {
                _logger.LogWarning(
                    "Automatic Tagesabschluss could not close register {RegisterId}: {Kind}",
                    cashRegisterId,
                    closeResult.Kind);
            }
        }
    }

    private async Task TryPeriodClosingsAsync(
        string actorUserId,
        Guid cashRegisterId,
        DateTime businessDay,
        CancellationToken cancellationToken)
    {
        if (!AutoTagesabschlussCutoff.IsLastDayOfMonth(businessDay))
            return;

        try
        {
            var monthly = await _monatsbeleg.CreateMonatsbelegClosingAsync(
                    actorUserId,
                    new CreateMonatsbelegClosingRequest
                    {
                        CashRegisterId = cashRegisterId,
                        Year = businessDay.Year,
                        Month = businessDay.Month,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!monthly.Success)
            {
                _logger.LogInformation(
                    "Automatic Monatsbeleg skipped for register {RegisterId} {Year}-{Month}: {Error}",
                    cashRegisterId,
                    businessDay.Year,
                    businessDay.Month,
                    monthly.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Automatic Monatsbeleg failed for register {RegisterId}",
                cashRegisterId);
        }

        if (!AutoTagesabschlussCutoff.IsLastDayOfYear(businessDay))
            return;

        try
        {
            var yearly = await _jahresbeleg.CreateJahresbelegClosingAsync(
                    actorUserId,
                    new CreateJahresbelegClosingRequest
                    {
                        CashRegisterId = cashRegisterId,
                        Year = businessDay.Year,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!yearly.Success)
            {
                _logger.LogInformation(
                    "Automatic Jahresbeleg skipped for register {RegisterId} {Year}: {Error}",
                    cashRegisterId,
                    businessDay.Year,
                    yearly.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Automatic Jahresbeleg failed for register {RegisterId}",
                cashRegisterId);
        }
    }

    private async Task PublishOpenOrdersWarningAsync(
        Guid tenantId,
        string? tenantName,
        Guid cashRegisterId,
        string? registerNumber,
        string? location,
        DateTime businessDay,
        int openOrders,
        bool blocked,
        CancellationToken cancellationToken)
    {
        var label = FormatRegisterLabel(registerNumber, location);
        var title = blocked
            ? $"Automatischer Tagesabschluss blockiert — offene Bestellungen ({label})"
            : $"Offene Bestellungen bei automatischem Tagesabschluss — {label}";
        var description = blocked
            ? $"Kasse {label} (Mandant {tenantName ?? tenantId.ToString("D")}) hat {openOrders} offene Bestellung(en)/Tisch(e). Automatischer Tagesabschluss für {businessDay:yyyy-MM-dd} wurde blockiert."
            : $"Kasse {label} hat {openOrders} offene Bestellung(en)/Tisch(e). Automatischer Tagesabschluss für {businessDay:yyyy-MM-dd} wird mit Warnung fortgesetzt.";

        await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.DailyClosingOpenOrdersWarning,
                    Title: title,
                    Description: description,
                    DedupKey: $"daily_closing_open_orders:{cashRegisterId:N}:{businessDay:yyyyMMdd}",
                    ActorUserId: SystemActorUserId,
                    EntityType: "cash_register",
                    EntityId: cashRegisterId.ToString("D"),
                    Metadata: new Dictionary<string, object>
                    {
                        ["cashRegisterId"] = cashRegisterId.ToString("D"),
                        ["openOrdersCount"] = openOrders,
                        ["blocked"] = blocked,
                        ["viennaBusinessDay"] = businessDay.ToString("yyyy-MM-dd"),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FormatRegisterLabel(string? registerNumber, string? location)
    {
        var number = registerNumber?.Trim();
        var place = location?.Trim();
        if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(place))
            return $"{number} — {place}";
        return number ?? place ?? "Kasse";
    }
}
