using System.Security.Cryptography;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.CriticalActions;

public interface IApprovalService
{
    Task<(bool Ok, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> RequestApprovalAsync(
        string requesterUserId,
        CreateApprovalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ApprovalToken, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> ApproveAsync(
        Guid requestId,
        string approverUserId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> RejectAsync(
        Guid requestId,
        string approverUserId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalRequestDto>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalRequestDto>> ListHistoryAsync(
        ApprovalHistoryQuery query,
        CancellationToken cancellationToken = default);

    Task<ApprovalHistoryReportDto> GetHistoryReportAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<ApprovalRequestDto?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Requester claims the single-use token after Super Admin approval.</summary>
    Task<(bool Ok, string? ApprovalToken, string? ErrorCode, string? Message)> ClaimTokenAsync(
        Guid requestId,
        string requesterUserId,
        CancellationToken cancellationToken = default);
}

public sealed class ApprovalService : IApprovalService
{
    private const string ClaimCachePrefix = "critical-action:claim:";
    private const string TokenCachePrefix = "critical-action:token:";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityEventPublisher _activity;
    private readonly IDataDeletionNotificationSender _email;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<CriticalActionOptions> _options;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IActivityEventPublisher activity,
        IDataDeletionNotificationSender email,
        IMemoryCache cache,
        IOptionsMonitor<CriticalActionOptions> options,
        ILogger<ApprovalService> logger)
    {
        _db = db;
        _userManager = userManager;
        _activity = activity;
        _email = email;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<(bool Ok, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> RequestApprovalAsync(
        string requesterUserId,
        CreateApprovalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return (false, null, "AUTH_REQUIRED", "Authentication required.");

        if (!Enum.IsDefined(request.ActionType))
            return (false, null, "INVALID_ACTION_TYPE", "Unknown critical action type.");

        var now = DateTime.UtcNow;
        var actionType = request.ActionType.ToString();
        var pathHint = string.IsNullOrWhiteSpace(request.PathHint) ? null : request.PathHint.Trim();

        var duplicate = await _db.ApprovalRequests.AsNoTracking()
            .AnyAsync(
                r => r.RequestedBy == requesterUserId
                     && r.ActionType == actionType
                     && r.TenantId == request.TenantId
                     && r.Status == ApprovalRequestStatuses.Pending
                     && r.ExpiresAt > now,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            return (false, null, "ALREADY_PENDING", "A pending approval already exists for this action.");

        string? tenantName = null;
        if (request.TenantId is Guid tid && tid != Guid.Empty)
        {
            tenantName = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tid)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tenantName is null)
                return (false, null, "TENANT_NOT_FOUND", "Tenant not found.");
        }

        var entity = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId is Guid t && t != Guid.Empty ? t : null,
            RequestedBy = requesterUserId,
            ActionType = actionType,
            Payload = Truncate(request.Payload, 32_000),
            Status = ApprovalRequestStatuses.Pending,
            RequestedAt = now,
            ExpiresAt = now.AddHours(24),
            Reason = Truncate(request.Reason, 1000),
            PathHint = Truncate(pathHint, 512),
        };

        _db.ApprovalRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await NotifySuperAdminsAsync(entity, tenantName, cancellationToken).ConfigureAwait(false);

        var dto = await MapDtoAsync(entity, cancellationToken).ConfigureAwait(false);
        return (true, dto, null, null);
    }

    public async Task<(bool Ok, string? ApprovalToken, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> ApproveAsync(
        Guid requestId,
        string approverUserId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            return (false, null, null, "AUTH_REQUIRED", "Authentication required.");

        var entity = await _db.ApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return (false, null, null, "NOT_FOUND", "Approval request not found.");

        var now = DateTime.UtcNow;
        if (entity.ExpiresAt <= now)
        {
            entity.Status = ApprovalRequestStatuses.Expired;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (false, null, null, "EXPIRED", "Approval request has expired.");
        }

        if (!string.Equals(entity.Status, ApprovalRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return (false, null, null, "NOT_PENDING", "Approval request is not pending.");

        if (string.Equals(entity.RequestedBy, approverUserId, StringComparison.Ordinal)
            && !_options.CurrentValue.SuperAdminMaySelfApprove)
        {
            return (false, null, null, "SELF_APPROVAL_FORBIDDEN", "Requester cannot approve their own critical action.");
        }

        var approver = await _userManager.FindByIdAsync(approverUserId).ConfigureAwait(false);
        if (approver is null)
            return (false, null, null, "USER_NOT_FOUND", "Approver not found.");

        var roles = await _userManager.GetRolesAsync(approver).ConfigureAwait(false);
        if (!roles.Any(r => string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
            return (false, null, null, "FORBIDDEN", "Only SuperAdmin may approve critical actions.");

        if (!Enum.TryParse<CriticalActionType>(entity.ActionType, ignoreCase: true, out var actionType))
            return (false, null, null, "INVALID_ACTION_TYPE", "Stored action type is invalid.");

        entity.Status = ApprovalRequestStatuses.Approved;
        entity.ApprovedBy = approverUserId;
        entity.ApprovedAt = now;
        entity.Notes = Truncate(notes, 2000);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var token = IssueToken(new CriticalActionApprovalPayload
        {
            UserId = entity.RequestedBy,
            ActionType = actionType,
            PathHint = entity.PathHint ?? string.Empty,
            ExpiresAtUtc = now.Add(GetTokenTtl()),
            IssuedByUserId = approverUserId,
        });

        _cache.Set(ClaimCachePrefix + entity.Id.ToString("D"), token, GetTokenTtl());

        if (entity.TenantId is Guid tenantId)
        {
            await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.CriticalActionApprovalApproved,
                metadata: new
                {
                    RequestId = entity.Id.ToString("D"),
                    ActionType = entity.ActionType,
                    ApproverUserId = approverUserId,
                },
                actorUserId: approverUserId,
                dedupKey: $"critical-approval-approved:{entity.Id:D}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Critical approval {RequestId} approved by {ApproverId} for {RequesterId} action {ActionType}",
            entity.Id,
            approverUserId,
            entity.RequestedBy,
            entity.ActionType);

        var dto = await MapDtoAsync(entity, cancellationToken).ConfigureAwait(false);
        return (true, token, dto, null, null);
    }

    public async Task<(bool Ok, ApprovalRequestDto? Dto, string? ErrorCode, string? Message)> RejectAsync(
        Guid requestId,
        string approverUserId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            return (false, null, "AUTH_REQUIRED", "Authentication required.");

        var entity = await _db.ApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return (false, null, "NOT_FOUND", "Approval request not found.");

        if (!string.Equals(entity.Status, ApprovalRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return (false, null, "NOT_PENDING", "Approval request is not pending.");

        var approver = await _userManager.FindByIdAsync(approverUserId).ConfigureAwait(false);
        if (approver is null)
            return (false, null, "USER_NOT_FOUND", "Approver not found.");

        var roles = await _userManager.GetRolesAsync(approver).ConfigureAwait(false);
        if (!roles.Any(r => string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
            return (false, null, "FORBIDDEN", "Only SuperAdmin may reject critical actions.");

        entity.Status = ApprovalRequestStatuses.Rejected;
        entity.ApprovedBy = approverUserId;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.Notes = Truncate(notes, 2000);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (entity.TenantId is Guid tenantId)
        {
            await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.CriticalActionApprovalRejected,
                metadata: new
                {
                    RequestId = entity.Id.ToString("D"),
                    ActionType = entity.ActionType,
                    ApproverUserId = approverUserId,
                },
                actorUserId: approverUserId,
                dedupKey: $"critical-approval-rejected:{entity.Id:D}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var dto = await MapDtoAsync(entity, cancellationToken).ConfigureAwait(false);
        return (true, dto, null, null);
    }

    public async Task<IReadOnlyList<ApprovalRequestDto>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        await ExpirePendingAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var rows = await _db.ApprovalRequests.AsNoTracking()
            .Where(r => r.Status == ApprovalRequestStatuses.Pending && r.ExpiresAt > now)
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<ApprovalRequestDto>(rows.Count);
        foreach (var row in rows)
            result.Add(await MapDtoAsync(row, cancellationToken).ConfigureAwait(false));
        return result;
    }

    public async Task<IReadOnlyList<ApprovalRequestDto>> ListHistoryAsync(
        ApprovalHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        await ExpirePendingAsync(cancellationToken).ConfigureAwait(false);

        var limit = Math.Clamp(query.Limit <= 0 ? 100 : query.Limit, 1, 500);
        var offset = Math.Max(0, query.Offset);
        var q = BuildHistoryBaseQuery(query.TenantId, query.Status, query.ActionType, query.FromUtc, query.ToUtc);

        var rows = await q
            .OrderByDescending(r => r.RequestedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<ApprovalRequestDto>(rows.Count);
        foreach (var row in rows)
            result.Add(await MapDtoAsync(row, cancellationToken).ConfigureAwait(false));
        return result;
    }

    public async Task<ApprovalHistoryReportDto> GetHistoryReportAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        await ExpirePendingAsync(cancellationToken).ConfigureAwait(false);

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);
        if (from > to)
            (from, to) = (to, from);

        var q = BuildHistoryBaseQuery(tenantId, status: null, actionType: null, from, to);
        var rows = await q.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        var approvalDurations = rows
            .Where(r =>
                r.ApprovedAt is not null
                && (string.Equals(r.Status, ApprovalRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Status, ApprovalRequestStatuses.Consumed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Status, ApprovalRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase)))
            .Select(r => (r.ApprovedAt!.Value - r.RequestedAt).TotalMinutes)
            .Where(m => m >= 0)
            .OrderBy(m => m)
            .ToList();

        var byAction = rows
            .GroupBy(r => r.ActionType)
            .Select(g => new ApprovalActionTypeCountDto
            {
                ActionType = g.Key,
                Count = g.Count(),
                ApprovedCount = g.Count(x =>
                    string.Equals(x.Status, ApprovalRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Status, ApprovalRequestStatuses.Consumed, StringComparison.OrdinalIgnoreCase)),
                RejectedCount = g.Count(x =>
                    string.Equals(x.Status, ApprovalRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase)),
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var recentEntities = rows
            .OrderByDescending(r => r.RequestedAt)
            .Take(50)
            .ToList();
        var recent = new List<ApprovalRequestDto>(recentEntities.Count);
        foreach (var row in recentEntities)
            recent.Add(await MapDtoAsync(row, cancellationToken).ConfigureAwait(false));

        return new ApprovalHistoryReportDto
        {
            FromUtc = from,
            ToUtc = to,
            TotalRequests = rows.Count,
            PendingCount = rows.Count(r =>
                string.Equals(r.Status, ApprovalRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase)),
            ApprovedCount = rows.Count(r =>
                string.Equals(r.Status, ApprovalRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Status, ApprovalRequestStatuses.Consumed, StringComparison.OrdinalIgnoreCase)),
            RejectedCount = rows.Count(r =>
                string.Equals(r.Status, ApprovalRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase)),
            ExpiredCount = rows.Count(r =>
                string.Equals(r.Status, ApprovalRequestStatuses.Expired, StringComparison.OrdinalIgnoreCase)),
            ConsumedCount = rows.Count(r =>
                string.Equals(r.Status, ApprovalRequestStatuses.Consumed, StringComparison.OrdinalIgnoreCase)),
            AverageTimeToApprovalMinutes = approvalDurations.Count == 0
                ? null
                : Math.Round(approvalDurations.Average(), 1),
            MedianTimeToApprovalMinutes = approvalDurations.Count == 0
                ? null
                : Math.Round(Median(approvalDurations), 1),
            ByActionType = byAction,
            Recent = recent,
        };
    }

    public async Task<ApprovalRequestDto?> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var row = await _db.ApprovalRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : await MapDtoAsync(row, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(bool Ok, string? ApprovalToken, string? ErrorCode, string? Message)> ClaimTokenAsync(
        Guid requestId,
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return (false, null, "AUTH_REQUIRED", "Authentication required.");

        var entity = await _db.ApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return (false, null, "NOT_FOUND", "Approval request not found.");

        if (!string.Equals(entity.RequestedBy, requesterUserId, StringComparison.Ordinal))
            return (false, null, "FORBIDDEN", "Only the requester may claim this approval token.");

        if (!string.Equals(entity.Status, ApprovalRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            return (false, null, "NOT_APPROVED", "Approval request is not approved.");

        var claimKey = ClaimCachePrefix + entity.Id.ToString("D");
        if (!_cache.TryGetValue(claimKey, out string? token) || string.IsNullOrWhiteSpace(token))
            return (false, null, "TOKEN_UNAVAILABLE", "Approval token is unavailable or already claimed.");

        _cache.Remove(claimKey);
        entity.Status = ApprovalRequestStatuses.Consumed;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (true, token, null, null);
    }

    private async Task NotifySuperAdminsAsync(
        ApprovalRequest entity,
        string? tenantName,
        CancellationToken cancellationToken)
    {
        var tenantId = entity.TenantId ?? Guid.Empty;
        if (tenantId != Guid.Empty)
        {
            await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.CriticalActionApprovalRequested,
                metadata: new
                {
                    RequestId = entity.Id.ToString("D"),
                    ActionType = entity.ActionType,
                    Reason = entity.Reason,
                    PathHint = entity.PathHint,
                    ExpiresAt = entity.ExpiresAt,
                },
                actorUserId: entity.RequestedBy,
                dedupKey: $"critical-approval-requested:{entity.Id:D}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var requester = await _userManager.FindByIdAsync(entity.RequestedBy).ConfigureAwait(false);
        var requesterLabel = requester?.Email
            ?? requester?.UserName
            ?? entity.RequestedBy;

        var subject = $"Critical Action Approval Required: {entity.ActionType}";
        var body =
            $"""
            A critical action requires your approval:

            Action: {entity.ActionType}
            Tenant: {tenantName ?? entity.TenantId?.ToString("D") ?? "N/A"}
            Requested By: {requesterLabel}
            Reason: {entity.Reason ?? "(none)"}
            Path: {entity.PathHint ?? "(none)"}
            Details: {Truncate(entity.Payload, 500) ?? "(none)"}

            Please review and approve/reject this action in the admin panel (/admin/approvals).

            Expires in 24 hours ({entity.ExpiresAt:u} UTC).
            """;

        try
        {
            var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
            var emails = superAdmins
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (emails.Count == 0)
            {
                _logger.LogInformation(
                    "Critical approval request {RequestId}: no SuperAdmin email recipients",
                    entity.Id);
                return;
            }

            await _email.SendAsync(
                to: emails,
                cc: Array.Empty<string>(),
                subject: subject,
                plainBody: body,
                ct: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to email SuperAdmins for critical approval {RequestId}", entity.Id);
        }
    }

    private string IssueToken(CriticalActionApprovalPayload payload)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var ttl = payload.ExpiresAtUtc - DateTime.UtcNow;
        if (ttl < TimeSpan.FromSeconds(30))
            ttl = TimeSpan.FromSeconds(30);

        _cache.Set(TokenCachePrefix + token, payload, ttl);
        return token;
    }

    private TimeSpan GetTokenTtl()
    {
        var minutes = Math.Clamp(_options.CurrentValue.ApprovalTokenTtlMinutes, 2, 30);
        return TimeSpan.FromMinutes(minutes);
    }

    private async Task<ApprovalRequestDto> MapDtoAsync(ApprovalRequest entity, CancellationToken cancellationToken)
    {
        string? tenantName = null;
        string? tenantSlug = null;
        if (entity.TenantId is Guid tid)
        {
            var tenant = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tid)
                .Select(t => new { t.Name, t.Slug })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            tenantName = tenant?.Name;
            tenantSlug = tenant?.Slug;
        }

        var requester = await _userManager.FindByIdAsync(entity.RequestedBy).ConfigureAwait(false);
        ApplicationUser? approver = null;
        if (!string.IsNullOrWhiteSpace(entity.ApprovedBy))
            approver = await _userManager.FindByIdAsync(entity.ApprovedBy).ConfigureAwait(false);

        int? timeToDecision = null;
        if (entity.ApprovedAt is DateTime decidedAt)
        {
            var minutes = (int)Math.Round((decidedAt - entity.RequestedAt).TotalMinutes);
            if (minutes >= 0)
                timeToDecision = minutes;
        }

        return new ApprovalRequestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = tenantName,
            TenantSlug = tenantSlug,
            RequestedBy = entity.RequestedBy,
            RequestedByEmail = requester?.Email,
            RequestedByDisplayName = requester?.UserName ?? requester?.Email,
            ApprovedBy = entity.ApprovedBy,
            ApprovedByEmail = approver?.Email,
            ApprovedByDisplayName = approver?.UserName ?? approver?.Email,
            ActionType = entity.ActionType,
            Payload = entity.Payload,
            Status = entity.Status,
            RequestedAt = entity.RequestedAt,
            ApprovedAt = entity.ApprovedAt,
            ExpiresAt = entity.ExpiresAt,
            Reason = entity.Reason,
            Notes = entity.Notes,
            PathHint = entity.PathHint,
            TimeToDecisionMinutes = timeToDecision,
        };
    }

    private async Task ExpirePendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.ApprovalRequests
            .Where(r => r.Status == ApprovalRequestStatuses.Pending && r.ExpiresAt <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (expired.Count == 0)
            return;

        foreach (var row in expired)
            row.Status = ApprovalRequestStatuses.Expired;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<ApprovalRequest> BuildHistoryBaseQuery(
        Guid? tenantId,
        string? status,
        string? actionType,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var q = _db.ApprovalRequests.AsNoTracking().AsQueryable();
        if (tenantId is Guid tid && tid != Guid.Empty)
            q = q.Where(r => r.TenantId == tid);
        if (!string.IsNullOrWhiteSpace(status) && ApprovalRequestStatuses.IsValid(status))
        {
            var normalized = ApprovalRequestStatuses.All
                .First(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
            q = q.Where(r => r.Status == normalized);
        }
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var action = actionType.Trim();
            q = q.Where(r => r.ActionType == action);
        }
        if (fromUtc is DateTime from)
            q = q.Where(r => r.RequestedAt >= from);
        if (toUtc is DateTime to)
            q = q.Where(r => r.RequestedAt <= to);
        return q;
    }

    private static double Median(IReadOnlyList<double> sortedAscending)
    {
        var n = sortedAscending.Count;
        if (n == 0)
            return 0;
        if (n % 2 == 1)
            return sortedAscending[n / 2];
        return (sortedAscending[n / 2 - 1] + sortedAscending[n / 2]) / 2.0;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
