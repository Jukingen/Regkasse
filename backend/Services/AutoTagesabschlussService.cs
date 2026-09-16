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

        if (!AutoTagesabschlussCutoff.IsPastCutoff(utcNow, settings.HourVienna, settings.MinuteVienna))
            return 0;

        var businessDay = AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utcNow);
        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(businessDay);
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);

        var registers = await _context.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location, r.CurrentUserId, r.CurrentBalance, r.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var closed = 0;
        foreach (var register in registers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var alreadyClosed = await _context.DailyClosings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    d =>
                        d.TenantId == tenantId
                        && d.CashRegisterId == register.Id
                        && d.ClosingType == "Daily"
                        && d.ClosingDate == closingAnchorUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (alreadyClosed)
                continue;

            var hasTransactions = await _context.Invoices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    i =>
                        i.TenantId == tenantId
                        && i.CashRegisterId == register.Id
                        && i.CreatedAt >= dayStartUtc
                        && i.CreatedAt < dayEndExclusiveUtc
                        && i.Status == InvoiceStatus.Paid,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!hasTransactions)
                continue;

            var openOrders = await CountOpenOrdersAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var policy = AutoTagesabschlussOpenOrdersPolicies.Normalize(settings.OpenOrdersPolicy);
            if (openOrders > 0)
            {
                await PublishOpenOrdersWarningAsync(
                        tenantId,
                        tenantName,
                        register.Id,
                        register.RegisterNumber,
                        register.Location,
                        businessDay,
                        openOrders,
                        blocked: policy == AutoTagesabschlussOpenOrdersPolicies.Block,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (policy == AutoTagesabschlussOpenOrdersPolicies.Block)
                {
                    _logger.LogWarning(
                        "Automatic Tagesabschluss blocked for register {RegisterId} tenant {TenantId}: {OpenOrders} open order(s).",
                        register.Id,
                        tenantId,
                        openOrders);
                    continue;
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
                var result = await _tagesabschluss.PerformDailyClosingAsync(
                        actorUserId,
                        register.Id,
                        new DailyClosingPerformOptions
                        {
                            Trigger = DailyClosingTriggers.Automatic,
                            CashCountNote = cashNote,
                            CashCount = cashCount,
                            CashDifference = cashDifference,
                            OpenOrdersCount = openOrders,
                            OpenOrdersForced = openOrders > 0
                                && policy != AutoTagesabschlussOpenOrdersPolicies.NotifyAndContinue,
                        },
                        businessDay,
                        AutoTagesabschlussSettings.AutomaticLateReason)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Automatic Tagesabschluss did not succeed for register {RegisterId}: {Error}",
                        register.Id,
                        result.ErrorMessage);
                    continue;
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
            finally
            {
                _tenantAccessor.TenantId = previousTenant;
            }
        }

        return closed;
    }

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

    private async Task<int> CountOpenOrdersAsync(Guid tenantId, CancellationToken cancellationToken)
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
                    && o.Status != TableOrderStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);

        var openCarts = await _context.Carts
            .AsNoTracking()
            .CountAsync(
                c =>
                    userIds.Contains(c.UserId)
                    && c.IsActive
                    && c.Status == CartStatus.Active,
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
