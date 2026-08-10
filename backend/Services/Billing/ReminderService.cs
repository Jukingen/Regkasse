using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Billing;

public class ReminderService : IReminderService, IBillingReminderService
{
    private const int MaxPageSize = 100;
    private static readonly int[] DefaultReminderAnchors = [30, 15, 7, 3, 1];

    private readonly AppDbContext _dbContext;
    private readonly ITenantLicenseService _tenantLicenseService;
    private readonly IOptions<BillingOptions> _billingOptions;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        AppDbContext dbContext,
        ITenantLicenseService tenantLicenseService,
        IOptions<BillingOptions> billingOptions,
        ILogger<ReminderService> logger)
    {
        _dbContext = dbContext;
        _tenantLicenseService = tenantLicenseService;
        _billingOptions = billingOptions;
        _logger = logger;
    }

    public async Task CheckAndCreateRemindersAsync(CancellationToken ct = default)
    {
        var db = _dbContext;
        var anchors = ResolveReminderAnchors();
        var scanDays = anchors.Length == 0 ? 30 : Math.Max(anchors.Max(), 1);

        var now = DateTime.UtcNow;
        var expiring = await _tenantLicenseService.GetExpiringLicensesAsync(scanDays, ct)
            .ConfigureAwait(false);

        var created = 0;

        foreach (var license in expiring)
        {
            var daysRemaining = (int)Math.Ceiling((license.ValidUntilUtc - now).TotalDays);
            foreach (var days in anchors)
            {
                // Exact calendar-day match (same rule as mandant LicenseReminderService).
                if (daysRemaining != days)
                    continue;

                var reminderDate = license.ValidUntilUtc.Date.AddDays(-days);
                var exists = await db.LicenseReminders
                    .AnyAsync(
                        r => r.LicenseSaleId == license.LicenseSaleId
                             && r.ReminderType == LicenseReminderTypes.Expiry
                             && r.ReminderDateUtc == reminderDate,
                        ct)
                    .ConfigureAwait(false);

                if (exists)
                    continue;

                db.LicenseReminders.Add(new LicenseReminder
                {
                    Id = Guid.NewGuid(),
                    TenantId = license.TenantId,
                    LicenseSaleId = license.LicenseSaleId,
                    ReminderDateUtc = reminderDate,
                    ReminderType = LicenseReminderTypes.Expiry,
                    Status = LicenseReminderStatuses.Pending,
                });
                created++;

                _logger.LogDebug(
                    "Created reminder for tenant {TenantSlug}: expiry-{Days}d at {Date}",
                    license.TenantSlug,
                    days,
                    reminderDate);
            }
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Created {Count} license reminders", created);
        }
    }

    public async Task SendPendingRemindersAsync(CancellationToken ct = default)
    {
        var db = _dbContext;

        var pending = await db.LicenseReminders
            .Include(r => r.Tenant)
            .Where(r => r.Status == LicenseReminderStatuses.Pending && r.ReminderSentAtUtc == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (pending.Count == 0)
            return;

        await EnrichMissingSalesAsync(db, pending, ct).ConfigureAwait(false);

        foreach (var reminder in pending)
        {
            reminder.ReminderSentAtUtc = DateTime.UtcNow;
            reminder.Status = LicenseReminderStatuses.Sent;

            _logger.LogInformation(
                "Reminder marked sent (no email path) for tenant {TenantSlug}: {Type}",
                reminder.Tenant?.Slug,
                reminder.ReminderType);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<LicenseReminderResponse>> GetForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var tenantExists = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);

        if (!tenantExists)
            throw new KeyNotFoundException($"Tenant {tenantId} not found");

        var reminders = await db.LicenseReminders
            .AsNoTracking()
            .Include(r => r.Tenant)
            .Include(r => r.LicenseSale)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.ReminderDateUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnrichMissingSalesAsync(db, reminders, ct).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        return reminders.Select(r => MapToResponse(r, now)).ToList();
    }

    public async Task MarkAsSentAsync(Guid reminderId, CancellationToken ct = default)
    {
        var db = _dbContext;

        var reminder = await db.LicenseReminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Reminder {reminderId} not found");

        reminder.ReminderSentAtUtc = DateTime.UtcNow;
        reminder.Status = LicenseReminderStatuses.Sent;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ScheduleRemindersForSaleAsync(Guid saleId, CancellationToken ct = default)
    {
        var db = _dbContext;

        var sale = await db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            .ConfigureAwait(false);

        if (sale == null || sale.Status != LicenseSaleStatuses.Active)
            return;

        var now = DateTime.UtcNow;
        if (sale.ValidUntilUtc <= now)
            return;

        var created = 0;

        foreach (var days in ResolveReminderAnchors())
        {
            var reminderDate = sale.ValidUntilUtc.Date.AddDays(-days);
            if (reminderDate <= now.Date)
                continue;

            var exists = await db.LicenseReminders
                .AnyAsync(
                    r => r.LicenseSaleId == sale.Id
                         && r.ReminderType == LicenseReminderTypes.Expiry
                         && r.ReminderDateUtc == reminderDate,
                    ct)
                .ConfigureAwait(false);

            if (exists)
                continue;

            db.LicenseReminders.Add(new LicenseReminder
            {
                Id = Guid.NewGuid(),
                TenantId = sale.TenantId,
                LicenseSaleId = sale.Id,
                ReminderDateUtc = reminderDate,
                ReminderType = LicenseReminderTypes.Expiry,
                Status = LicenseReminderStatuses.Pending,
            });
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task CancelRemindersForSaleAsync(Guid saleId, CancellationToken ct = default)
    {
        var db = _dbContext;

        var pending = await db.LicenseReminders
            .Where(r => r.LicenseSaleId == saleId && r.Status == LicenseReminderStatuses.Pending)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (pending.Count == 0)
            return;

        foreach (var reminder in pending)
            reminder.Status = LicenseReminderStatuses.Cancelled;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var db = _dbContext;

        var count = await db.LicenseReminders
            .CountAsync(
                r => r.Status == LicenseReminderStatuses.Pending && r.ReminderSentAtUtc == null,
                ct)
            .ConfigureAwait(false);

        if (count == 0)
            return 0;

        await SendPendingRemindersAsync(ct).ConfigureAwait(false);
        return count;
    }

    public async Task<BillingReminderListResponse> ListAsync(
        BillingReminderQuery query,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        IQueryable<LicenseReminder> remindersQuery = db.LicenseReminders
            .AsNoTracking()
            .Include(r => r.Tenant)
            .Include(r => r.LicenseSale);

        if (query.TenantId.HasValue)
            remindersQuery = remindersQuery.Where(r => r.TenantId == query.TenantId.Value);

        if (query.LicenseSaleId.HasValue)
            remindersQuery = remindersQuery.Where(r => r.LicenseSaleId == query.LicenseSaleId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && LicenseReminderStatuses.IsValid(query.Status))
        {
            remindersQuery = remindersQuery.Where(r => r.Status == query.Status.Trim());
        }

        if (query.FromDate.HasValue)
            remindersQuery = remindersQuery.Where(r => r.ReminderDateUtc >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            remindersQuery = remindersQuery.Where(r => r.ReminderDateUtc <= query.ToDate.Value);

        var totalCount = await remindersQuery.CountAsync(ct).ConfigureAwait(false);
        var rows = await remindersQuery
            .OrderBy(r => r.ReminderDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnrichMissingSalesAsync(db, rows, ct).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new BillingReminderListResponse
        {
            Items = rows.Select(r => MapToResponse(r, now)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }

    private int[] ResolveReminderAnchors()
    {
        var configured = _billingOptions.Value.ReminderDaysBeforeExpiry;
        if (configured is { Length: > 0 })
            return configured.Where(d => d > 0).Distinct().OrderByDescending(d => d).ToArray();

        return DefaultReminderAnchors;
    }

    private static async Task EnrichMissingSalesAsync(
        AppDbContext db,
        List<LicenseReminder> reminders,
        CancellationToken ct)
    {
        var missingSaleIds = reminders
            .Where(r => r.LicenseSale == null)
            .Select(r => r.LicenseSaleId)
            .Distinct()
            .ToList();

        if (missingSaleIds.Count == 0)
            return;

        var salesById = await db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => missingSaleIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct)
            .ConfigureAwait(false);

        foreach (var reminder in reminders)
        {
            if (reminder.LicenseSale == null && salesById.TryGetValue(reminder.LicenseSaleId, out var sale))
                reminder.LicenseSale = sale;
        }
    }

    private static LicenseReminderResponse MapToResponse(LicenseReminder r, DateTime now)
    {
        var validUntil = r.LicenseSale?.ValidUntilUtc ?? now;
        var daysRemaining = r.LicenseSale == null
            ? 0
            : (validUntil - now).Days;

        return new LicenseReminderResponse
        {
            Id = r.Id,
            TenantId = r.TenantId,
            TenantName = r.Tenant?.Name ?? "Unknown",
            TenantSlug = r.Tenant?.Slug ?? "Unknown",
            LicenseSaleId = r.LicenseSaleId,
            LicenseKey = r.LicenseSale?.LicenseKey ?? "Unknown",
            ValidUntilUtc = validUntil,
            ReminderDateUtc = r.ReminderDateUtc,
            ReminderSentAtUtc = r.ReminderSentAtUtc,
            ReminderType = r.ReminderType,
            Status = r.Status,
            DaysRemaining = daysRemaining,
        };
    }
}
