using KasseAPI_Final;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Reminder;

/// <summary>
/// Hourly worker: in the Europe/Vienna evening window, publishes activity/email reminders
/// for cash registers that still need Tagesabschluss. Does <strong>not</strong> auto-close (RKSV).
/// </summary>
public sealed class TagesabschlussReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TagesabschlussReminderOptions> _options;
    private readonly ILogger<TagesabschlussReminderService> _logger;

    public TagesabschlussReminderService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TagesabschlussReminderOptions> options,
        ILogger<TagesabschlussReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opt = _options.CurrentValue;
                var intervalMinutes = Math.Max(5, opt.CheckIntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);

                if (!opt.Enabled)
                    continue;

                if (!TagesabschlussReminderWindow.IsInsideReminderWindow(DateTime.UtcNow, opt))
                    continue;

                await SendRemindersAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Tagesabschluss reminder hosted service iteration failed.");
            }
        }
    }

    internal async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var tenantAccessor = sp.GetRequiredService<ICurrentTenantAccessor>();
        tenantAccessor.TenantId = null;

        var db = sp.GetRequiredService<AppDbContext>();
        var activity = sp.GetRequiredService<IActivityEventService>();
        var opt = _options.CurrentValue;

        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday);
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                var (tenantSent, tenantSkipped) = await SendTenantRemindersAsync(
                        db,
                        activity,
                        tenant.Id,
                        tenant.Name,
                        viennaToday,
                        closingAnchorUtc,
                        dayStartUtc,
                        dayEndExclusiveUtc,
                        opt,
                        cancellationToken)
                    .ConfigureAwait(false);
                sent += tenantSent;
                skipped += tenantSkipped;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Tagesabschluss reminder failed for tenant {TenantId}",
                    tenant.Id);
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "Tagesabschluss reminders published: sent={Sent} skipped={Skipped}",
                sent,
                skipped);
        }
    }

    private static async Task<(int Sent, int Skipped)> SendTenantRemindersAsync(
        AppDbContext db,
        IActivityEventService activity,
        Guid tenantId,
        string? tenantName,
        DateTime viennaToday,
        DateTime closingAnchorUtc,
        DateTime dayStartUtc,
        DateTime dayEndExclusiveUtc,
        TagesabschlussReminderOptions options,
        CancellationToken cancellationToken)
    {
        var registers = await db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;

        foreach (var register in registers)
        {
            var alreadyClosed = await db.DailyClosings
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
            {
                skipped++;
                continue;
            }

            if (options.RequireTransactions)
            {
                var hasTransactions = await db.PaymentDetails
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(
                        p =>
                            p.CashRegisterId == register.Id
                            && p.IsActive
                            && p.RksvSpecialReceiptKind == null
                            && p.CreatedAt >= dayStartUtc
                            && p.CreatedAt < dayEndExclusiveUtc,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!hasTransactions)
                {
                    skipped++;
                    continue;
                }
            }

            var dedupKey = TagesabschlussReminderWindow.BuildDedupKey(register.Id, viennaToday);
            var alreadyReminded = await db.ActivityEvents
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    e => e.TenantId == tenantId && e.DedupKey == dedupKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (alreadyReminded)
            {
                skipped++;
                continue;
            }

            var registerLabel = FormatRegisterLabel(register.RegisterNumber, register.Location);
            var title = $"Tagesabschluss steht aus — {registerLabel}";
            var description =
                $"Der Tagesabschluss für Kasse {registerLabel} (Mandant {tenantName ?? tenantId.ToString("D")}) " +
                $"wurde am {viennaToday:yyyy-MM-dd} (Europe/Vienna) noch nicht durchgeführt. " +
                "Bitte manuell abschließen und Kassenbestand zählen. Automatischer Abschluss ist nicht möglich.";

            await activity.PublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        ActivityEventType.DailyClosingPendingReminder,
                        Title: title,
                        Description: description,
                        DedupKey: dedupKey,
                        EntityType: "cash_register",
                        EntityId: register.Id.ToString("D"),
                        Metadata: new Dictionary<string, object>
                        {
                            ["cashRegisterId"] = register.Id.ToString("D"),
                            ["registerNumber"] = register.RegisterNumber ?? "",
                            ["viennaBusinessDay"] = viennaToday.ToString("yyyy-MM-dd"),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            sent++;
        }

        return (sent, skipped);
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
