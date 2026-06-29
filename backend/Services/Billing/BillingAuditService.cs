using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Billing;

public class BillingAuditService : IBillingAuditService
{
    private const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BillingAuditService> _logger;

    public BillingAuditService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<BillingAuditService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public Task LogAsync(
        string action,
        Guid? tenantId,
        Guid? saleId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default) =>
        WriteAuditAsync(action, _currentUserService.GetCurrentUserId(), tenantId, saleId, details, ipAddress, ct);

    public Task LogAsync(
        string action,
        Guid actorUserId,
        Guid? tenantId,
        Guid? saleId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default) =>
        WriteAuditAsync(action, actorUserId, tenantId, saleId, details, ipAddress, ct);

    public async Task<BillingAuditLogListResponse> ListAsync(
        BillingAuditLogQuery query,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var auditQuery = db.BillingAuditLogs
            .AsNoTracking()
            .Include(l => l.Tenant)
            .Include(l => l.Sale)
            .AsQueryable();

        if (query.TenantId.HasValue)
            auditQuery = auditQuery.Where(l => l.TenantId == query.TenantId.Value);

        if (query.SaleId.HasValue)
            auditQuery = auditQuery.Where(l => l.SaleId == query.SaleId.Value);

        if (!string.IsNullOrEmpty(query.Action))
            auditQuery = auditQuery.Where(l => l.Action == query.Action);

        if (query.FromDate.HasValue)
            auditQuery = auditQuery.Where(l => l.TimestampUtc >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            auditQuery = auditQuery.Where(l => l.TimestampUtc <= query.ToDate.Value);

        if (!string.IsNullOrEmpty(query.UserId) && Guid.TryParse(query.UserId, out var filterUserId))
            auditQuery = auditQuery.Where(l => l.UserId == filterUserId);

        var totalCount = await auditQuery.CountAsync(ct).ConfigureAwait(false);

        var items = await auditQuery
            .OrderByDescending(l => l.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnrichMissingSalesAsync(db, items, ct).ConfigureAwait(false);
        var userNames = await LoadUserDisplayNamesAsync(db, items, ct).ConfigureAwait(false);

        return new BillingAuditLogListResponse
        {
            Items = items.Select(log => MapToResponse(log, userNames)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize),
        };
    }

    public async Task<List<BillingAuditLogResponse>> GetForSaleAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var logs = await db.BillingAuditLogs
            .AsNoTracking()
            .Include(l => l.Tenant)
            .Include(l => l.Sale)
            .Where(l => l.SaleId == saleId)
            .OrderByDescending(l => l.TimestampUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnrichMissingSalesAsync(db, logs, ct).ConfigureAwait(false);
        var userNames = await LoadUserDisplayNamesAsync(db, logs, ct).ConfigureAwait(false);

        return logs.Select(log => MapToResponse(log, userNames)).ToList();
    }

    public async Task<List<BillingAuditLogResponse>> GetForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var db = _dbContext;

        var logs = await db.BillingAuditLogs
            .AsNoTracking()
            .Include(l => l.Tenant)
            .Include(l => l.Sale)
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.TimestampUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        await EnrichMissingSalesAsync(db, logs, ct).ConfigureAwait(false);
        var userNames = await LoadUserDisplayNamesAsync(db, logs, ct).ConfigureAwait(false);

        return logs.Select(log => MapToResponse(log, userNames)).ToList();
    }

    public Task LogLicenseSoldAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        LogSaleEventAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.SaleCreated,
            cancellationReason: null,
            ipAddress,
            cancellationToken);

    public Task LogLicenseCancelledAsync(
        LicenseSale sale,
        Guid actorUserId,
        string cancellationReason,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        LogSaleEventAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.SaleCancelled,
            cancellationReason,
            ipAddress,
            cancellationToken);

    public Task LogLicenseActivatedAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        LogSaleEventAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.LicenseActivated,
            cancellationReason: null,
            ipAddress,
            cancellationToken);

    public Task LogLicenseExtendedAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        LogSaleEventAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.LicenseExtended,
            cancellationReason: null,
            ipAddress,
            cancellationToken);

    private async Task WriteAuditAsync(
        string action,
        Guid userId,
        Guid? tenantId,
        Guid? saleId,
        string? details,
        string? ipAddress,
        CancellationToken ct)
    {
        try
        {
            var db = _dbContext;

            var audit = new BillingAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Action = action,
                SaleId = saleId,
                Details = details,
                IpAddress = ipAddress,
                TimestampUtc = DateTime.UtcNow,
            };

            db.BillingAuditLogs.Add(audit);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogDebug("Billing audit logged: {Action} for tenant {TenantId}", action, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Billing audit log write failed Action={Action} SaleId={SaleId}",
                action,
                saleId);
        }
    }

    private Task LogSaleEventAsync(
        LicenseSale sale,
        Guid actorUserId,
        string action,
        string? cancellationReason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new BillingAuditDetails(
            sale.PriceNet,
            sale.PriceGross,
            sale.Currency,
            sale.InvoiceNumber,
            sale.LicenseKey,
            sale.LicensePlan,
            cancellationReason), JsonOptions);

        return WriteAuditAsync(
            action,
            actorUserId,
            sale.TenantId,
            sale.Id,
            details,
            ipAddress,
            cancellationToken);
    }

    private static async Task EnrichMissingSalesAsync(
        AppDbContext db,
        List<BillingAuditLog> logs,
        CancellationToken ct)
    {
        var missingSaleIds = logs
            .Where(l => l.SaleId.HasValue && l.Sale == null)
            .Select(l => l.SaleId!.Value)
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

        foreach (var log in logs)
        {
            if (log.SaleId.HasValue && log.Sale == null && salesById.TryGetValue(log.SaleId.Value, out var sale))
                log.Sale = sale;
        }
    }

    private static async Task<Dictionary<Guid, string>> LoadUserDisplayNamesAsync(
        AppDbContext db,
        IReadOnlyList<BillingAuditLog> logs,
        CancellationToken ct)
    {
        var userIds = logs
            .Select(l => l.UserId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(id => id.ToString("D"))
            .ToList();

        if (userIds.Count == 0)
            return new Dictionary<Guid, string>();

        var users = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return users.ToDictionary(
            u => Guid.Parse(u.Id),
            u =>
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                return string.IsNullOrWhiteSpace(name) ? u.UserName ?? u.Id : name;
            });
    }

    private BillingAuditLogResponse MapToResponse(
        BillingAuditLog log,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        userNames.TryGetValue(log.UserId, out var userName);

        return new BillingAuditLogResponse
        {
            Id = log.Id,
            TenantId = log.TenantId,
            TenantName = log.Tenant?.Name,
            TenantSlug = log.Tenant?.Slug,
            UserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName,
            Action = log.Action,
            SaleId = log.SaleId,
            InvoiceNumber = log.Sale?.InvoiceNumber,
            Details = log.Details,
            IpAddress = log.IpAddress,
            TimestampUtc = log.TimestampUtc,
        };
    }

    private sealed record BillingAuditDetails(
        decimal PriceNet,
        decimal PriceGross,
        string Currency,
        string InvoiceNumber,
        string LicenseKey,
        string LicensePlan,
        string? CancellationReason);
}
