using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PermissionRequestService : IPermissionRequestService
{
    public const string InvalidPermissionCode = "INVALID_PERMISSION";
    public const string SystemCriticalBlockedCode = "SYSTEM_CRITICAL_BLOCKED";
    public const string AlreadyPendingCode = "PERMISSION_REQUEST_ALREADY_PENDING";
    public const string NotFoundCode = "PERMISSION_REQUEST_NOT_FOUND";
    public const string InvalidStatusCode = "PERMISSION_REQUEST_INVALID_STATUS";
    public const string InvalidDurationCode = "INVALID_DURATION";
    public const string OverrideFailedCode = "OVERRIDE_UPSERT_FAILED";

    private readonly AppDbContext _db;
    private readonly IUserPermissionOverrideService _overrides;
    private readonly IActivityEventPublisher _activity;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly TimeProvider _time;
    private readonly ILogger<PermissionRequestService> _logger;

    public PermissionRequestService(
        AppDbContext db,
        IUserPermissionOverrideService overrides,
        IActivityEventPublisher activity,
        UserManager<ApplicationUser> userManager,
        IUserSessionInvalidation sessionInvalidation,
        TimeProvider time,
        ILogger<PermissionRequestService> logger)
    {
        _db = db;
        _overrides = overrides;
        _activity = activity;
        _userManager = userManager;
        _sessionInvalidation = sessionInvalidation;
        _time = time;
        _logger = logger;
    }

    public async Task<PermissionRequestMutationResult> CreateAsync(
        string requesterUserId,
        Guid? tenantId,
        CreatePermissionRequestBody body,
        CancellationToken cancellationToken = default)
    {
        var permission = (body.Permission ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(permission) || !PermissionCatalogMetadata.IsValidPermissionKey(permission))
        {
            return Fail(InvalidPermissionCode, "Permission key is invalid.");
        }

        if (IsSystemCritical(permission))
        {
            return Fail(SystemCriticalBlockedCode, "system.critical cannot be requested.");
        }

        var duration = (body.Duration ?? PermissionRequestDurations.SevenDays).Trim().ToLowerInvariant();
        if (duration is not (PermissionRequestDurations.OneDay
            or PermissionRequestDurations.SevenDays
            or PermissionRequestDurations.ThirtyDays
            or PermissionRequestDurations.Custom))
        {
            return Fail(InvalidDurationCode, "Duration must be 1d, 7d, 30d, or custom.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (duration == PermissionRequestDurations.Custom && !body.CustomExpiresAt.HasValue)
        {
            return Fail(InvalidDurationCode, "CustomExpiresAt is required for custom duration.");
        }

        var expiresAt = PermissionRequestDurations.ResolveExpiresAt(duration, now, body.CustomExpiresAt);
        if (expiresAt <= now)
        {
            return Fail(InvalidDurationCode, "Requested expiry must be in the future.");
        }

        var reason = (body.Reason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail("VALIDATION_ERROR", "Reason is required.");
        }

        var pendingExists = await _db.PermissionRequests.AnyAsync(
            r => r.RequesterUserId == requesterUserId
                 && r.Permission == permission
                 && r.TenantId == tenantId
                 && r.Status == PermissionRequestStatuses.Pending,
            cancellationToken);
        if (pendingExists)
        {
            return Fail(AlreadyPendingCode, "A pending request already exists for this permission.");
        }

        var entity = new PermissionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequesterUserId = requesterUserId,
            Permission = permission,
            Reason = reason.Length > 500 ? reason[..500] : reason,
            RequestedDuration = duration,
            RequestedExpiresAt = expiresAt,
            Status = PermissionRequestStatuses.Pending,
            RequestedAt = now,
        };

        _db.PermissionRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            entity.TenantId,
            ActivityEventType.PermissionRequested,
            entity,
            requesterUserId,
            $"permission-request:{entity.Id}",
            cancellationToken);

        _logger.LogInformation(
            "Permission request created {RequestId} user={UserId} permission={Permission}",
            entity.Id,
            requesterUserId,
            permission);

        return new PermissionRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    public async Task<IReadOnlyList<PermissionRequestDto>> ListMineAsync(
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PermissionRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == requesterUserId)
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionRequestDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PermissionRequests.AsNoTracking()
            .Where(r => r.Status == PermissionRequestStatuses.Pending)
            .OrderBy(r => r.RequestedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<PermissionRequestMutationResult> ApproveAsync(
        Guid requestId,
        string resolverUserId,
        ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.PermissionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity is null)
            return Fail(NotFoundCode, "Permission request not found.");

        if (!string.Equals(entity.Status, PermissionRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Fail(InvalidStatusCode, "Only pending requests can be approved.");

        if (IsSystemCritical(entity.Permission))
            return Fail(SystemCriticalBlockedCode, "system.critical cannot be granted via request.");

        var now = _time.GetUtcNow().UtcDateTime;
        var expiresAt = body?.ExpiresAt ?? entity.RequestedExpiresAt ?? now.AddDays(7);
        if (expiresAt <= now)
            return Fail(InvalidDurationCode, "ExpiresAt must be in the future.");

        var upserted = await _overrides.UpsertOverrideAsync(
            entity.RequesterUserId,
            new UpsertUserPermissionOverrideRequest
            {
                Permission = entity.Permission,
                IsGranted = true,
                Reason = entity.Reason,
                ExpiresAt = expiresAt,
                TenantId = entity.TenantId,
            },
            resolverUserId,
            entity.TenantId,
            cancellationToken);

        if (upserted is null)
            return Fail(OverrideFailedCode, "Failed to create permission override.");

        entity.Status = PermissionRequestStatuses.Approved;
        entity.ResolvedByUserId = resolverUserId;
        entity.ResolvedAt = now;
        entity.ResolutionNote = string.IsNullOrWhiteSpace(body?.Note) ? null : body.Note.Trim();
        entity.ResultingOverrideId = upserted.Id;
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            entity.TenantId,
            ActivityEventType.PermissionRequestApproved,
            entity,
            resolverUserId,
            $"permission-request-approved:{entity.Id}",
            cancellationToken);

        await _sessionInvalidation.InvalidateSessionsForUserAsync(entity.RequesterUserId, cancellationToken);

        return new PermissionRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    public async Task<PermissionRequestMutationResult> RejectAsync(
        Guid requestId,
        string resolverUserId,
        ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.PermissionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity is null)
            return Fail(NotFoundCode, "Permission request not found.");

        if (!string.Equals(entity.Status, PermissionRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Fail(InvalidStatusCode, "Only pending requests can be rejected.");

        entity.Status = PermissionRequestStatuses.Rejected;
        entity.ResolvedByUserId = resolverUserId;
        entity.ResolvedAt = _time.GetUtcNow().UtcDateTime;
        entity.ResolutionNote = string.IsNullOrWhiteSpace(body?.Note) ? null : body.Note.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            entity.TenantId,
            ActivityEventType.PermissionRequestRejected,
            entity,
            resolverUserId,
            $"permission-request-rejected:{entity.Id}",
            cancellationToken);

        return new PermissionRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    public async Task<PermissionRequestStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var grouped = await _db.PermissionRequests.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Count(string status) =>
            grouped.FirstOrDefault(g => string.Equals(g.Status, status, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

        return new PermissionRequestStatsDto
        {
            Pending = Count(PermissionRequestStatuses.Pending),
            Approved = Count(PermissionRequestStatuses.Approved),
            Rejected = Count(PermissionRequestStatuses.Rejected),
            Total = grouped.Sum(g => g.Count),
        };
    }

    private static bool IsSystemCritical(string permission) =>
        string.Equals(permission, AppPermissions.SystemCritical, StringComparison.OrdinalIgnoreCase)
        || string.Equals(permission, "system.critical", StringComparison.OrdinalIgnoreCase);

    private async Task PublishActivityAsync(
        Guid? tenantId,
        ActivityEventType type,
        PermissionRequest entity,
        string? actorUserId,
        string dedupKey,
        CancellationToken cancellationToken)
    {
        if (!tenantId.HasValue)
            return;

        await _activity.TryPublishAsync(
            tenantId.Value,
            type,
            new
            {
                RequestId = entity.Id.ToString(),
                entity.Permission,
                entity.RequesterUserId,
                entity.Status,
                Message = $"Permission request {entity.Status}: {entity.Permission}",
            },
            actorUserId,
            dedupKey,
            cancellationToken);
    }

    private async Task<IReadOnlyList<PermissionRequestDto>> MapManyAsync(
        IReadOnlyList<PermissionRequest> rows,
        CancellationToken cancellationToken)
    {
        var userIds = rows.Select(r => r.RequesterUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName, cancellationToken);

        return rows.Select(r =>
        {
            names.TryGetValue(r.RequesterUserId, out var userName);
            return Map(r, userName);
        }).ToList();
    }

    private async Task<PermissionRequestDto> MapAsync(PermissionRequest entity, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(entity.RequesterUserId);
        return Map(entity, user?.UserName);
    }

    private static PermissionRequestDto Map(PermissionRequest entity, string? userName) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        RequesterUserId = entity.RequesterUserId,
        RequesterUserName = userName,
        Permission = entity.Permission,
        Reason = entity.Reason,
        RequestedDuration = entity.RequestedDuration,
        RequestedExpiresAt = entity.RequestedExpiresAt,
        Status = entity.Status,
        RequestedAt = entity.RequestedAt,
        ResolvedByUserId = entity.ResolvedByUserId,
        ResolvedAt = entity.ResolvedAt,
        ResolutionNote = entity.ResolutionNote,
        ResultingOverrideId = entity.ResultingOverrideId,
    };

    private static PermissionRequestMutationResult Fail(string code, string error) => new()
    {
        Succeeded = false,
        Code = code,
        Error = error,
    };
}
