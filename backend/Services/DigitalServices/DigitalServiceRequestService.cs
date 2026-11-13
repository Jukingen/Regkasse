using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DigitalServices;

public sealed class DigitalServiceRequestService : IDigitalServiceRequestService
{
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string InvalidServiceTypeCode = "INVALID_SERVICE_TYPE";
    public const string AlreadyPendingCode = "DIGITAL_REQUEST_ALREADY_PENDING";
    public const string RequestNotFoundCode = "DIGITAL_REQUEST_NOT_FOUND";
    public const string InvalidStatusCode = "DIGITAL_REQUEST_INVALID_STATUS";

    private readonly AppDbContext _db;
    private readonly ITenantServiceStatusService _statuses;
    private readonly IActivityEventPublisher _activity;
    private readonly TimeProvider _time;
    private readonly ILogger<DigitalServiceRequestService> _logger;

    public DigitalServiceRequestService(
        AppDbContext db,
        ITenantServiceStatusService statuses,
        IActivityEventPublisher activity,
        TimeProvider time,
        ILogger<DigitalServiceRequestService> logger)
    {
        _db = db;
        _statuses = statuses;
        _activity = activity;
        _time = time;
        _logger = logger;
    }

    public async Task<DigitalServiceRequestResponseDto> CreateAsync(
        Guid tenantId,
        string serviceType,
        string? note,
        string? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = (serviceType ?? string.Empty).Trim();
        if (!TenantServiceTypes.IsValid(normalizedType))
        {
            return Fail(InvalidServiceTypeCode, "ServiceType must be website or app.");
        }

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null,
                cancellationToken);
        if (tenant is null)
            return Fail(TenantNotFoundCode, "Tenant not found.");

        var pendingExists = await _db.DigitalServiceRequests
            .IgnoreQueryFilters()
            .AnyAsync(
                r => r.TenantId == tenantId
                     && r.ServiceType == normalizedType
                     && r.Status == DigitalServiceRequestStatuses.Pending,
                cancellationToken);
        if (pendingExists)
        {
            return Fail(
                AlreadyPendingCode,
                "A pending request already exists for this service type.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entity = new DigitalServiceRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceType = normalizedType,
            Status = DigitalServiceRequestStatuses.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAt = now,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };

        _db.DigitalServiceRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _statuses.MarkRequestPendingAsync(tenantId, normalizedType, cancellationToken);

        _logger.LogInformation(
            "Digital service request created: {RequestId} tenant={TenantId} type={ServiceType}",
            entity.Id,
            tenantId,
            normalizedType);

        await _activity.TryPublishAsync(
            tenantId,
            ActivityEventType.DigitalServiceRequested,
            new
            {
                RequestId = entity.Id.ToString(),
                TenantName = tenant.Name,
                TenantSlug = tenant.Slug,
                ServiceType = normalizedType,
                Message = $"{tenant.Name} requested {normalizedType} creation.",
            },
            actorUserId: requestedByUserId,
            dedupKey: $"digital-request:{entity.Id}",
            cancellationToken: cancellationToken);

        return new DigitalServiceRequestResponseDto
        {
            Succeeded = true,
            Request = Map(entity, tenant.Name, tenant.Slug),
        };
    }

    public async Task<IReadOnlyList<DigitalServiceRequestDto>> ListAsync(
        string? status = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DigitalServiceRequests.AsNoTracking().IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(r => r.Status == normalizedStatus);
        }

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        var rows = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        return rows.Select(r =>
        {
            tenants.TryGetValue(r.TenantId, out var tenant);
            return Map(r, tenant?.Name, tenant?.Slug);
        }).ToList();
    }

    public Task<DigitalServiceRequestResponseDto> ApproveAsync(
        Guid requestId,
        string? resolvedByUserId,
        string? note,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(requestId, DigitalServiceRequestStatuses.Approved, resolvedByUserId, note, cancellationToken);

    public Task<DigitalServiceRequestResponseDto> RejectAsync(
        Guid requestId,
        string? resolvedByUserId,
        string? note,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(requestId, DigitalServiceRequestStatuses.Rejected, resolvedByUserId, note, cancellationToken);

    private async Task<DigitalServiceRequestResponseDto> ResolveAsync(
        Guid requestId,
        string newStatus,
        string? resolvedByUserId,
        string? note,
        CancellationToken cancellationToken)
    {
        var entity = await _db.DigitalServiceRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity is null)
            return Fail(RequestNotFoundCode, "Digital service request not found.");

        if (!string.Equals(entity.Status, DigitalServiceRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(InvalidStatusCode, "Only pending requests can be resolved.");
        }

        entity.Status = newStatus;
        entity.ResolvedByUserId = resolvedByUserId;
        entity.ResolvedAt = _time.GetUtcNow().UtcDateTime;
        entity.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        if (string.Equals(newStatus, DigitalServiceRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            await _statuses.MarkRequestRejectedAsync(entity.TenantId, entity.ServiceType, cancellationToken);
        else
            await _statuses.ClearPendingRequestAsync(entity.TenantId, entity.ServiceType, cancellationToken);

        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == entity.TenantId)
            .Select(t => new { t.Name, t.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogInformation(
            "Digital service request {Status}: {RequestId} tenant={TenantId}",
            newStatus,
            entity.Id,
            entity.TenantId);

        return new DigitalServiceRequestResponseDto
        {
            Succeeded = true,
            Request = Map(entity, tenant?.Name, tenant?.Slug),
        };
    }

    private static DigitalServiceRequestDto Map(
        DigitalServiceRequest entity,
        string? tenantName,
        string? tenantSlug) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = tenantName,
            TenantSlug = tenantSlug,
            ServiceType = entity.ServiceType,
            Status = entity.Status,
            RequestedByUserId = entity.RequestedByUserId,
            RequestedAt = entity.RequestedAt,
            Note = entity.Note,
            ResolvedByUserId = entity.ResolvedByUserId,
            ResolvedAt = entity.ResolvedAt,
            ResolutionNote = entity.ResolutionNote,
        };

    private static DigitalServiceRequestResponseDto Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error,
        };
}
