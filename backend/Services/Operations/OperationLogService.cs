using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Operations;

public sealed class OperationLogService : IOperationLogService
{
    private static readonly HashSet<string> UndoableTypes = new(StringComparer.Ordinal)
    {
        OperationTypes.UpdateProduct,
        OperationTypes.UpdateCustomer,
        OperationTypes.CreateCategory,
        OperationTypes.CreateVoucher,
    };

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OperationLogService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsUndoable(string operationType) =>
        !string.IsNullOrWhiteSpace(operationType) && UndoableTypes.Contains(operationType);

    public async Task<OperationLog> LogAsync(
        Guid tenantId,
        string userId,
        string operationType,
        string entityType,
        string entityId,
        object? beforeState,
        object? afterState,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type is required.", nameof(operationType));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity id is required.", nameof(entityId));

        var http = _httpContextAccessor.HttpContext;
        var entry = new OperationLog
        {
            TenantId = tenantId,
            UserId = userId,
            OperationType = operationType.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            BeforeState = beforeState is null ? null : OperationSnapshots.Serialize(beforeState),
            AfterState = afterState is null ? null : OperationSnapshots.Serialize(afterState),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            IpAddress = Truncate(http?.Connection.RemoteIpAddress?.ToString(), 64),
            UserAgent = Truncate(http?.Request.Headers.UserAgent.ToString(), 512),
        };

        _db.OperationLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<OperationLogListResponseDto> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? operationType = null,
        bool? isUndone = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.OperationLogs.AsNoTracking().Where(o => o.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(operationType))
            query = query.Where(o => o.OperationType == operationType);
        if (isUndone.HasValue)
            query = query.Where(o => o.IsUndone == isUndone.Value);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.UserName, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byId = users.ToDictionary(u => u.Id, StringComparer.Ordinal);

        var items = rows.Select(r =>
        {
            byId.TryGetValue(r.UserId, out var user);
            var display = user is null
                ? null
                : $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(display))
                display = user?.UserName ?? user?.Email;

            return new OperationLogListItemDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                UserId = r.UserId,
                UserEmail = user?.Email,
                UserDisplayName = display,
                OperationType = r.OperationType,
                EntityType = r.EntityType,
                EntityId = r.EntityId,
                IsUndone = r.IsUndone,
                UndoneAt = r.UndoneAt,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                CanUndo = !r.IsUndone && IsUndoable(r.OperationType),
            };
        }).ToList();

        return new OperationLogListResponseDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<OperationLogDetailDto?> GetAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var r = await _db.OperationLogs.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == operationId && o.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (r is null)
            return null;

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == r.UserId)
            .Select(u => new { u.Email, u.UserName, u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var display = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(display))
            display = user?.UserName ?? user?.Email;

        return new OperationLogDetailDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            UserId = r.UserId,
            UserEmail = user?.Email,
            UserDisplayName = display,
            OperationType = r.OperationType,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            IsUndone = r.IsUndone,
            UndoneAt = r.UndoneAt,
            UndoneBy = r.UndoneBy,
            Reason = r.Reason,
            CreatedAt = r.CreatedAt,
            CanUndo = !r.IsUndone && IsUndoable(r.OperationType),
            BeforeState = r.BeforeState,
            AfterState = r.AfterState,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }
}
