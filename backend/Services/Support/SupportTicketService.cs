using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Support;

public sealed class SupportTicketService : ISupportTicketService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly ISupportTicketNotificationService _notify;
    private readonly ILogger<SupportTicketService> _logger;

    public SupportTicketService(
        AppDbContext db,
        ISupportTicketNotificationService notify,
        ILogger<SupportTicketService> logger)
    {
        _db = db;
        _notify = notify;
        _logger = logger;
    }

    public async Task<SupportTicketDetailDto> CreateAsync(
        Guid tenantId,
        string userId,
        string? displayName,
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new KeyNotFoundException("Ticket not found.");

        var category = (request.Category ?? string.Empty).Trim();
        var priority = string.IsNullOrWhiteSpace(request.Priority)
            ? SupportTicketPriorities.Medium
            : request.Priority.Trim();
        var title = request.ResolvedSubject();
        var message = (request.Message ?? string.Empty).Trim();

        if (!SupportTicketCategories.IsValid(category))
            throw new ArgumentException("Invalid ticket category.", nameof(request));
        if (!SupportTicketPriorities.IsValid(priority))
            throw new ArgumentException("Invalid ticket priority.", nameof(request));
        if (title.Length < 3 || message.Length < 10)
            throw new ArgumentException("Title or message is too short.", nameof(request));

        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            TenantId = tenantId,
            TicketNumber = await NextTicketNumberAsync(cancellationToken).ConfigureAwait(false),
            Category = category,
            Priority = priority,
            Status = SupportTicketStatuses.Open,
            Title = Truncate(title, 200) ?? title,
            Message = Truncate(message, 4000) ?? message,
            CreatedByUserId = userId,
            CreatedByDisplayName = Truncate(displayName, 200),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        ticket.Messages.Add(new SupportTicketMessage
        {
            TenantId = tenantId,
            AuthorUserId = userId,
            AuthorDisplayName = Truncate(displayName, 200),
            Body = Truncate(message, 4000) ?? message,
            IsStaffReply = false,
            IsInternal = false,
            CreatedAtUtc = now,
        });

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Support ticket created. TicketId={TicketId} TicketNumber={TicketNumber} TenantId={TenantId}",
            ticket.Id,
            ticket.TicketNumber,
            tenantId);
        await _notify.NotifyNewTicketAsync(ticket, cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return await LoadDetailAsync(ticket.Id, tenantId, includeInternal: false, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Ticket not found.");
    }

    public Task<SupportTicketListResponse> ListForTenantAsync(
        Guid tenantId,
        SupportTicketListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Task.FromResult(EmptyPage(query));

        return ListAsync(
            TicketsQuery().Where(t => t.TenantId == tenantId),
            query,
            cancellationToken);
    }

    public Task<SupportTicketListResponse> ListAllAsync(
        SupportTicketListQuery? query = null,
        CancellationToken cancellationToken = default) =>
        ListAsync(TicketsQuery(), query, cancellationToken);

    public async Task<SupportTicketInboxSummaryDto> GetInboxSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await TicketsQuery()
            .Select(t => new { t.Status, t.Category, t.Priority, t.ResolvedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var since = DateTime.UtcNow.AddDays(-30);
        return new SupportTicketInboxSummaryDto
        {
            OpenCount = rows.Count(r => r.Status == SupportTicketStatuses.Open),
            InProgressCount = rows.Count(r =>
                r.Status is SupportTicketStatuses.InProgress
                    or SupportTicketStatuses.WaitingOnTenant
                    or SupportTicketStatuses.WaitingOnStaff),
            ResolvedCount = rows.Count(r => r.Status == SupportTicketStatuses.Resolved),
            ResolvedLast30DaysCount = rows.Count(r =>
                r.Status == SupportTicketStatuses.Resolved
                && r.ResolvedAtUtc is DateTime resolved
                && resolved >= since),
            ClosedCount = rows.Count(r => r.Status == SupportTicketStatuses.Closed),
            TotalCount = rows.Count,
            ByCategory = rows
                .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            ByPriority = rows
                .GroupBy(r => r.Priority, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
        };
    }

    public async Task<SupportTicketDetailDto> GetForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || ticketId == Guid.Empty)
            throw new KeyNotFoundException("Ticket not found.");

        var dto = await LoadDetailAsync(ticketId, tenantId, includeInternal: false, cancellationToken)
            .ConfigureAwait(false);
        return dto ?? throw new KeyNotFoundException("Ticket not found.");
    }

    public async Task<SupportTicketDetailDto> GetAnyAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId == Guid.Empty)
            throw new KeyNotFoundException("Ticket not found.");

        return await LoadDetailAsync(ticketId, tenantId: null, includeInternal: true, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Ticket not found.");
    }

    public Task<SupportTicketDetailDto> AddMessageForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        string userId,
        string? displayName,
        string body,
        CancellationToken cancellationToken = default) =>
        AddMessageAsync(
            tenantId,
            ticketId,
            userId,
            displayName,
            body,
            isStaff: false,
            isInternal: false,
            cancellationToken);

    public Task<SupportTicketDetailDto> AddStaffMessageAsync(
        Guid ticketId,
        string userId,
        string? displayName,
        string body,
        bool isInternal,
        CancellationToken cancellationToken = default) =>
        AddMessageAsync(
            tenantId: null,
            ticketId,
            userId,
            displayName,
            body,
            isStaff: true,
            isInternal,
            cancellationToken);

    public Task<SupportTicketDetailDto> UpdateStatusAsync(
        Guid ticketId,
        string status,
        CancellationToken cancellationToken = default) =>
        UpdateStatusCoreAsync(ticketId, tenantId: null, status, tenantRestricted: false, cancellationToken);

    public Task<SupportTicketDetailDto> UpdateStatusForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        string status,
        CancellationToken cancellationToken = default) =>
        UpdateStatusCoreAsync(ticketId, tenantId, status, tenantRestricted: true, cancellationToken);

    public async Task<SupportTicketDetailDto> AssignTicketAsync(
        Guid ticketId,
        string assignedToUserId,
        string? assignedToDisplayName,
        CancellationToken cancellationToken = default)
    {
        var userId = (assignedToUserId ?? string.Empty).Trim();
        if (userId.Length == 0)
            throw new ArgumentException("Assigned user is required.", nameof(assignedToUserId));

        var ticket = await LoadTrackedTicketAsync(ticketId, tenantId: null, cancellationToken)
            .ConfigureAwait(false);

        ticket.AssignedToUserId = Truncate(userId, 450) ?? userId;
        ticket.AssignedToDisplayName = Truncate(assignedToDisplayName, 200);
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        if (ticket.Status == SupportTicketStatuses.Open)
            ticket.Status = SupportTicketStatuses.InProgress;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return await LoadDetailAsync(ticket.Id, tenantId: null, includeInternal: true, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Ticket not found.");
    }

    public async Task<int> GetOpenTicketCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return 0;

        return await TicketsQuery()
            .Where(t =>
                t.TenantId == tenantId
                && t.Status != SupportTicketStatuses.Resolved
                && t.Status != SupportTicketStatuses.Closed)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SupportTicketDetailDto> AddMessageAsync(
        Guid? tenantId,
        Guid ticketId,
        string userId,
        string? displayName,
        string body,
        bool isStaff,
        bool isInternal,
        CancellationToken cancellationToken)
    {
        var text = (body ?? string.Empty).Trim();
        if (text.Length < 1)
            throw new ArgumentException("Message is required.", nameof(body));

        var ticket = await LoadTrackedTicketAsync(ticketId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        _db.SupportTicketMessages.Add(new SupportTicketMessage
        {
            TicketId = ticket.Id,
            TenantId = ticket.TenantId,
            AuthorUserId = userId,
            AuthorDisplayName = Truncate(displayName, 200),
            Body = Truncate(text, 4000) ?? text,
            IsStaffReply = isStaff,
            IsInternal = isStaff && isInternal,
            CreatedAtUtc = now,
        });
        ticket.UpdatedAtUtc = now;

        if (!isInternal)
        {
            ticket.Status = isStaff
                ? SupportTicketStatuses.WaitingOnTenant
                : SupportTicketStatuses.WaitingOnStaff;
            if (ticket.ResolvedAtUtc is not null
                && ticket.Status is not SupportTicketStatuses.Resolved
                    and not SupportTicketStatuses.Closed)
            {
                ticket.ResolvedAtUtc = null;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (isStaff && !isInternal)
            await _notify.NotifyStaffReplyAsync(ticket, cancellationToken).ConfigureAwait(false);
        else if (!isStaff)
            await _notify.NotifyTenantReplyAsync(ticket, cancellationToken).ConfigureAwait(false);

        _db.ChangeTracker.Clear();
        return await LoadDetailAsync(
                ticket.Id,
                isStaff ? null : tenantId,
                includeInternal: isStaff,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Ticket not found.");
    }

    private async Task<SupportTicketDetailDto> UpdateStatusCoreAsync(
        Guid ticketId,
        Guid? tenantId,
        string status,
        bool tenantRestricted,
        CancellationToken cancellationToken)
    {
        var next = (status ?? string.Empty).Trim();
        if (!SupportTicketStatuses.IsValid(next))
            throw new ArgumentException("Invalid ticket status.", nameof(status));

        if (tenantRestricted
            && next is not SupportTicketStatuses.Open and not SupportTicketStatuses.Closed)
        {
            throw new ArgumentException("Tenants may only close or reopen tickets.", nameof(status));
        }

        var ticket = await LoadTrackedTicketAsync(ticketId, tenantId, cancellationToken)
            .ConfigureAwait(false);
        var previous = ticket.Status;
        ticket.Status = next;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        if (next == SupportTicketStatuses.Resolved)
            ticket.ResolvedAtUtc ??= DateTime.UtcNow;
        else if (next is SupportTicketStatuses.Open or SupportTicketStatuses.InProgress)
            ticket.ResolvedAtUtc = null;
        else if (next == SupportTicketStatuses.Closed && ticket.ResolvedAtUtc is null)
            ticket.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (next == SupportTicketStatuses.Resolved
            && !previous.Equals(SupportTicketStatuses.Resolved, StringComparison.OrdinalIgnoreCase))
        {
            await _notify.NotifyResolvedAsync(ticket, cancellationToken).ConfigureAwait(false);
        }
        else if (next == SupportTicketStatuses.Closed
            && !previous.Equals(SupportTicketStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            await _notify.NotifyClosedAsync(ticket, cancellationToken).ConfigureAwait(false);
        }

        _db.ChangeTracker.Clear();
        return await LoadDetailAsync(
                ticket.Id,
                tenantRestricted ? tenantId : null,
                includeInternal: !tenantRestricted,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Ticket not found.");
    }

    private async Task<SupportTicket> LoadTrackedTicketAsync(
        Guid ticketId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var ticket = await _db.SupportTickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);

        if (ticket is null || (tenantId is Guid required && ticket.TenantId != required))
            throw new KeyNotFoundException("Ticket not found.");

        return ticket;
    }

    private static async Task<SupportTicketListResponse> ListAsync(
        IQueryable<SupportTicket> query,
        SupportTicketListQuery? request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request?.Page ?? 1);
        var pageSize = request?.PageSize is int size && size > 0
            ? Math.Clamp(size, 1, MaxPageSize)
            : DefaultPageSize;

        if (!string.IsNullOrWhiteSpace(request?.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request?.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request?.Priority))
        {
            var priority = request.Priority.Trim();
            query = query.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(request?.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t =>
                t.TicketNumber.Contains(term)
                || t.Title.Contains(term)
                || (t.Tenant != null && t.Tenant.Name.Contains(term)));
        }

        if (request?.FromUtc is DateTime fromUtc)
            query = query.Where(t => t.CreatedAtUtc >= fromUtc);
        if (request?.ToUtc is DateTime toUtc)
            query = query.Where(t => t.CreatedAtUtc <= toUtc);

        var openCount = await query
            .CountAsync(
                t => t.Status != SupportTicketStatuses.Resolved
                    && t.Status != SupportTicketStatuses.Closed,
                cancellationToken)
            .ConfigureAwait(false);
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var rows = await query
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new SupportTicketListItemDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                TenantName = t.Tenant != null ? t.Tenant.Name : null,
                TicketNumber = t.TicketNumber,
                Category = t.Category,
                Priority = t.Priority,
                Status = t.Status,
                Title = t.Title,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByDisplayName = t.CreatedByDisplayName,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToDisplayName = t.AssignedToDisplayName,
                ResolvedAtUtc = t.ResolvedAtUtc,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
                MessageCount = t.Messages.Count,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SupportTicketListResponse
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            OpenCount = openCount,
        };
    }

    private IQueryable<SupportTicket> TicketsQuery() =>
        _db.SupportTickets.AsNoTracking().IgnoreQueryFilters().Include(t => t.Tenant);

    private async Task<SupportTicketDetailDto?> LoadDetailAsync(
        Guid ticketId,
        Guid? tenantId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var query = TicketsQuery().Include(t => t.Messages);
        var ticket = await query
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken)
            .ConfigureAwait(false);
        if (ticket is null || (tenantId is Guid required && ticket.TenantId != required))
            return null;

        var messages = ticket.Messages
            .Where(m => includeInternal || !m.IsInternal)
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .Select(m => new SupportTicketMessageDto
            {
                Id = m.Id,
                AuthorUserId = m.AuthorUserId,
                AuthorDisplayName = m.AuthorDisplayName,
                Body = m.Body,
                IsStaffReply = m.IsStaffReply,
                IsInternal = m.IsInternal,
                CreatedAtUtc = m.CreatedAtUtc,
            })
            .ToList();

        var item = MapListItem(ticket);
        return new SupportTicketDetailDto
        {
            Id = item.Id,
            TenantId = item.TenantId,
            TenantName = item.TenantName,
            TicketNumber = item.TicketNumber,
            Category = item.Category,
            Priority = item.Priority,
            Status = item.Status,
            Title = item.Title,
            Message = ticket.Message,
            CreatedByUserId = item.CreatedByUserId,
            CreatedByDisplayName = item.CreatedByDisplayName,
            AssignedToUserId = item.AssignedToUserId,
            AssignedToDisplayName = item.AssignedToDisplayName,
            ResolvedAtUtc = item.ResolvedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            MessageCount = messages.Count,
            Messages = messages,
        };
    }

    private static SupportTicketListItemDto MapListItem(SupportTicket t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        TenantName = t.Tenant?.Name,
        TicketNumber = t.TicketNumber,
        Category = t.Category,
        Priority = t.Priority,
        Status = t.Status,
        Title = t.Title,
        CreatedByUserId = t.CreatedByUserId,
        CreatedByDisplayName = t.CreatedByDisplayName,
        AssignedToUserId = t.AssignedToUserId,
        AssignedToDisplayName = t.AssignedToDisplayName,
        ResolvedAtUtc = t.ResolvedAtUtc,
        CreatedAtUtc = t.CreatedAtUtc,
        UpdatedAtUtc = t.UpdatedAtUtc,
        MessageCount = t.Messages.Count,
    };

    private async Task<string> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SUP-{year}-";
        for (var i = 0; i < 8; i++)
        {
            var last = await _db.SupportTickets
                .IgnoreQueryFilters()
                .Where(t => t.TicketNumber.StartsWith(prefix))
                .Select(t => t.TicketNumber)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var max = 0;
            foreach (var number in last)
            {
                var suffix = number.Length > prefix.Length ? number[prefix.Length..] : string.Empty;
                if (int.TryParse(suffix, out var seq) && seq > max)
                    max = seq;
            }

            var candidate = $"{prefix}{(max + 1):D4}";
            var exists = await _db.SupportTickets
                .IgnoreQueryFilters()
                .AnyAsync(t => t.TicketNumber == candidate, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
                return candidate;
        }

        return $"SUP-{year}-{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant();
    }

    private static SupportTicketListResponse EmptyPage(SupportTicketListQuery? query)
    {
        var page = Math.Max(1, query?.Page ?? 1);
        var pageSize = query?.PageSize is int size && size > 0
            ? Math.Clamp(size, 1, MaxPageSize)
            : DefaultPageSize;
        return new SupportTicketListResponse { Page = page, PageSize = pageSize };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
