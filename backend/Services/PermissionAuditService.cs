using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

public sealed class PermissionAuditEntryDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    /// <summary>created | updated | deleted | reverted</summary>
    public string Action { get; set; } = "updated";
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class PermissionAuditLogsResponse
{
    public List<PermissionAuditEntryDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class RevertPermissionAuditRequest
{
    public string? Reason { get; set; }
    public bool Force { get; set; }
}

public sealed class RevertPermissionAuditResponse
{
    public bool Success { get; set; }
    public Guid? RevertedAuditId { get; set; }
    public Guid? NewAuditId { get; set; }
    public int NewerChangesCount { get; set; }
    public bool WarningNewerChanges { get; set; }
    public string? Message { get; set; }
}

public sealed class AddPermissionAuditNoteRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string Note { get; set; } = string.Empty;
}

public interface IPermissionAuditService
{
    Task<PermissionAuditLogsResponse> GetPermissionAuditLogsAsync(
        string? roleId,
        string? roleName,
        string? permissionKey,
        string? actorUserId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Collect expanded permission audit rows (capped) for reports/exports.</summary>
    Task<IReadOnlyList<PermissionAuditEntryDto>> CollectEntriesAsync(
        string? roleId,
        string? roleName,
        string? permissionKey,
        string? actorUserId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxRows = 10_000,
        CancellationToken cancellationToken = default);

    Task<RevertPermissionAuditResponse> RevertAsync(
        Guid auditId,
        string actorUserId,
        string actorRole,
        string? reason,
        bool force,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> AddNoteAsync(
        Guid auditId,
        string actorUserId,
        string actorRole,
        string note,
        CancellationToken cancellationToken = default);
}

public sealed class PermissionAuditService : IPermissionAuditService
{
    private static readonly string[] PermissionActions =
    {
        AuditLogActions.ROLE_CREATE,
        AuditLogActions.ROLE_DELETE,
        AuditLogActions.ROLE_PERMISSIONS_UPDATE,
        AuditLogActions.USER_PERMISSION_OVERRIDES_CHANGED,
        "ROLE_PERMISSIONS_REVERT",
    };

    private const int MassPermissionChangeThreshold = 10;

    private readonly IAuditLogService _auditLogService;
    private readonly IRoleManagementService _roleManagementService;
    private readonly ILogger<PermissionAuditService> _logger;
    private readonly ActivityEventRecorder? _activityEvents;
    private readonly ICurrentTenantAccessor? _tenantAccessor;

    public PermissionAuditService(
        IAuditLogService auditLogService,
        IRoleManagementService roleManagementService,
        ILogger<PermissionAuditService> logger,
        ActivityEventRecorder? activityEvents = null,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        _auditLogService = auditLogService;
        _roleManagementService = roleManagementService;
        _logger = logger;
        _activityEvents = activityEvents;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<PermissionAuditLogsResponse> GetPermissionAuditLogsAsync(
        string? roleId,
        string? roleName,
        string? permissionKey,
        string? actorUserId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var list = await CollectEntriesAsync(
            roleId, roleName, permissionKey, actorUserId, fromDate, toDate, maxRows: 10_000, cancellationToken)
            .ConfigureAwait(false);

        var total = list.Count;
        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PermissionAuditLogsResponse
        {
            Items = pageItems.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
        };
    }

    public async Task<IReadOnlyList<PermissionAuditEntryDto>> CollectEntriesAsync(
        string? roleId,
        string? roleName,
        string? permissionKey,
        string? actorUserId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxRows = 10_000,
        CancellationToken cancellationToken = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 50_000);

        var filters = new AuditLogQueryFilters
        {
            StartDate = fromDate,
            EndDate = toDate,
            UserId = actorUserId,
            EntityType = string.IsNullOrWhiteSpace(roleName) && string.IsNullOrWhiteSpace(roleId)
                ? null
                : AuditLogEntityTypes.ROLE,
            Search = roleName,
        };

        var allExpanded = new List<PermissionAuditEntryDto>();
        foreach (var action in PermissionActions)
        {
            filters.Action = action;
            for (var page = 1; page <= 50 && allExpanded.Count < maxRows; page++)
            {
                var (items, _) = await _auditLogService.GetAuditLogsPagedAsync(
                    filters, pageSize: 100, page: page, includeTotalCount: false).ConfigureAwait(false);
                if (items.Count == 0)
                    break;
                foreach (var log in items)
                    allExpanded.AddRange(ExpandLog(log));
            }
        }

        IEnumerable<PermissionAuditEntryDto> query = allExpanded
            .OrderByDescending(e => e.Timestamp)
            .ThenBy(e => e.PermissionKey, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var rn = roleName.Trim();
            query = query.Where(e =>
                string.Equals(e.RoleName, rn, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            var rid = roleId.Trim();
            query = query.Where(e =>
                string.Equals(e.RoleId, rid, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(permissionKey))
        {
            var pk = permissionKey.Trim();
            query = query.Where(e =>
                e.PermissionKey.Contains(pk, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            var aid = actorUserId.Trim();
            query = query.Where(e =>
                string.Equals(e.ActorUserId, aid, StringComparison.OrdinalIgnoreCase));
        }

        return query.Take(maxRows).ToList();
    }

    public async Task<RevertPermissionAuditResponse> RevertAsync(
        Guid auditId,
        string actorUserId,
        string actorRole,
        string? reason,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var log = await _auditLogService.GetAuditLogByIdAsync(auditId).ConfigureAwait(false);
        if (log == null)
        {
            return new RevertPermissionAuditResponse
            {
                Success = false,
                Message = "Audit entry not found.",
            };
        }

        if (log.Action != AuditLogActions.ROLE_PERMISSIONS_UPDATE &&
            log.Action != "ROLE_PERMISSIONS_REVERT")
        {
            return new RevertPermissionAuditResponse
            {
                Success = false,
                Message = "Only role permission updates can be reverted automatically.",
            };
        }

        var oldPayload = ParseRolePermissions(log.OldValues);
        var newPayload = ParseRolePermissions(log.NewValues);
        var roleName = oldPayload.RoleName
            ?? newPayload.RoleName
            ?? log.EntityName;
        if (string.IsNullOrWhiteSpace(roleName) || oldPayload.Permissions == null)
        {
            return new RevertPermissionAuditResponse
            {
                Success = false,
                Message = "Previous permission snapshot missing.",
            };
        }

        var newerCount = await CountNewerRolePermissionChangesAsync(
            roleName, log.Timestamp, cancellationToken).ConfigureAwait(false);
        if (newerCount > 0 && !force)
        {
            return new RevertPermissionAuditResponse
            {
                Success = false,
                WarningNewerChanges = true,
                NewerChangesCount = newerCount,
                Message = $"There are {newerCount} newer permission change(s) for this role. Confirm with force=true to overwrite.",
            };
        }

        var roles = await _roleManagementService.GetRolesWithPermissionsAsync(cancellationToken)
            .ConfigureAwait(false);
        var current = roles
            .FirstOrDefault(r => string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase))
            ?.Permissions
            ?.ToArray()
            ?? Array.Empty<string>();
        var result = await _roleManagementService.SetRolePermissionsAsync(
            roleName, oldPayload.Permissions, cancellationToken).ConfigureAwait(false);

        if (result != SetRolePermissionsResult.Success)
        {
            return new RevertPermissionAuditResponse
            {
                Success = false,
                Message = result.ToString(),
            };
        }

        var revertLog = await _auditLogService.LogSystemOperationAsync(
            "ROLE_PERMISSIONS_REVERT",
            AuditLogEntityTypes.ROLE,
            actorUserId,
            actorRole,
            description: $"Permission change reverted (source audit {auditId})",
            notes: reason,
            actionType: AuditEventType.RolePermissionsUpdated,
            oldValues: new { roleName, permissions = current, revertedFromAuditId = auditId },
            newValues: new { roleName, permissions = oldPayload.Permissions, revertedFromAuditId = auditId },
            entityName: roleName).ConfigureAwait(false);

        _logger.LogInformation(
            "Permission audit {AuditId} reverted by {Actor} for role {Role}",
            auditId, actorUserId, roleName);

        await TryPublishRevertActivityAsync(
            roleName,
            current,
            oldPayload.Permissions,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return new RevertPermissionAuditResponse
        {
            Success = true,
            RevertedAuditId = auditId,
            NewAuditId = revertLog.Id,
            NewerChangesCount = newerCount,
            WarningNewerChanges = newerCount > 0,
            Message = "Reverted successfully.",
        };
    }

    private async Task TryPublishRevertActivityAsync(
        string roleName,
        string[] before,
        string[] after,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (_activityEvents == null)
            return;

        try
        {
            var oldSet = new HashSet<string>(before ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var newSet = new HashSet<string>(after ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var added = newSet.Where(k => !oldSet.Contains(k)).OrderBy(x => x).ToArray();
            var removed = oldSet.Where(k => !newSet.Contains(k)).OrderBy(x => x).ToArray();
            var changeTotal = added.Length + removed.Length;
            var parts = new List<string> { "Reverted to previous permission snapshot." };
            if (added.Length > 0)
                parts.Add($"Added: {string.Join(", ", added.Take(8))}{(added.Length > 8 ? "…" : "")}");
            if (removed.Length > 0)
                parts.Add($"Removed: {string.Join(", ", removed.Take(8))}{(removed.Length > 8 ? "…" : "")}");

            var isSuperAdminRole = string.Equals(roleName, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
            var isMass = changeTotal >= MassPermissionChangeThreshold;
            var type = isSuperAdminRole || isMass
                ? ActivityEventType.SystemPermissionChange
                : ActivityEventType.RolePermissionsUpdated;

            var tenantId = _tenantAccessor?.TenantId is Guid tid && tid != Guid.Empty
                ? tid
                : LegacyDefaultTenantIds.Primary;

            await _activityEvents.TryPublishAsync(
                tenantId,
                type,
                new
                {
                    RoleName = roleName,
                    ActorId = actorUserId,
                    AddedCount = added.Length,
                    RemovedCount = removed.Length,
                    WhatChanged = string.Join(" · ", parts),
                    IsMassUpdate = isMass,
                    IsSuperAdminRole = isSuperAdminRole,
                    IsRevert = true,
                },
                actorUserId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Permission revert activity publish failed for role {Role}", roleName);
        }
    }

    public async Task<(bool Ok, string? Error)> AddNoteAsync(
        Guid auditId,
        string actorUserId,
        string actorRole,
        string note,
        CancellationToken cancellationToken = default)
    {
        var log = await _auditLogService.GetAuditLogByIdAsync(auditId).ConfigureAwait(false);
        if (log == null) return (false, "Audit entry not found.");

        await _auditLogService.LogSystemOperationAsync(
            log.Action,
            log.EntityType,
            actorUserId,
            actorRole,
            description: $"Note on audit {auditId}",
            notes: note,
            actionType: log.ActionType,
            entityName: log.EntityName,
            requestData: new { relatedAuditId = auditId, note }).ConfigureAwait(false);

        return (true, null);
    }

    private async Task<int> CountNewerRolePermissionChangesAsync(
        string roleName,
        DateTime afterUtc,
        CancellationToken cancellationToken)
    {
        var filters = new AuditLogQueryFilters
        {
            StartDate = afterUtc.AddMilliseconds(1),
            EntityType = AuditLogEntityTypes.ROLE,
            Action = AuditLogActions.ROLE_PERMISSIONS_UPDATE,
            Search = roleName,
        };
        var (items, _) = await _auditLogService.GetAuditLogsPagedAsync(
            filters, pageSize: 50, page: 1, includeTotalCount: false).ConfigureAwait(false);
        return items.Count(i =>
            string.Equals(i.EntityName, roleName, StringComparison.OrdinalIgnoreCase)
            || (i.OldValues?.Contains(roleName, StringComparison.OrdinalIgnoreCase) ?? false)
            || (i.NewValues?.Contains(roleName, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static List<PermissionAuditEntryDto> ExpandLog(AuditLog log)
    {
        var action = MapAction(log.Action);
        var actorName = log.ActorDisplayName ?? log.UserRole ?? "System";
        var baseDto = new PermissionAuditEntryDto
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            ActorUserId = log.UserId ?? string.Empty,
            ActorName = actorName,
            ActorEmail = string.Empty,
            Action = action,
            RoleId = log.EntityId?.ToString() ?? string.Empty,
            RoleName = log.EntityName ?? string.Empty,
            Reason = log.Notes,
            IpAddress = log.IpAddress,
        };

        if (log.Action == AuditLogActions.ROLE_PERMISSIONS_UPDATE ||
            log.Action == "ROLE_PERMISSIONS_REVERT")
        {
            var oldP = ParseRolePermissions(log.OldValues);
            var newP = ParseRolePermissions(log.NewValues);
            baseDto.RoleName = oldP.RoleName ?? newP.RoleName ?? baseDto.RoleName;
            var oldSet = new HashSet<string>(oldP.Permissions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var newSet = new HashSet<string>(newP.Permissions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var keys = oldSet.Union(newSet).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            var rows = new List<PermissionAuditEntryDto>();
            foreach (var key in keys)
            {
                var inOld = oldSet.Contains(key);
                var inNew = newSet.Contains(key);
                if (inOld == inNew) continue;
                rows.Add(new PermissionAuditEntryDto
                {
                    Id = log.Id,
                    Timestamp = baseDto.Timestamp,
                    ActorUserId = baseDto.ActorUserId,
                    ActorName = baseDto.ActorName,
                    ActorEmail = baseDto.ActorEmail,
                    Action = log.Action == "ROLE_PERMISSIONS_REVERT" ? "reverted" : "updated",
                    RoleId = baseDto.RoleId,
                    RoleName = baseDto.RoleName,
                    PermissionKey = key,
                    OldValue = inOld ? "allowed" : "absent",
                    NewValue = inNew ? "allowed" : "absent",
                    Reason = baseDto.Reason,
                    IpAddress = baseDto.IpAddress,
                });
            }

            if (rows.Count == 0)
            {
                baseDto.PermissionKey = baseDto.RoleName;
                baseDto.OldValue = "defaults";
                baseDto.NewValue = "defaults";
                rows.Add(baseDto);
            }

            return rows;
        }

        if (log.Action == AuditLogActions.ROLE_CREATE)
        {
            var created = ParseRolePermissions(log.NewValues);
            baseDto.RoleName = created.RoleName ?? baseDto.RoleName;
            baseDto.PermissionKey = baseDto.RoleName;
            baseDto.OldValue = null;
            baseDto.NewValue = "defaults";
            return new List<PermissionAuditEntryDto> { baseDto };
        }

        if (log.Action == AuditLogActions.ROLE_DELETE)
        {
            var deleted = ParseRolePermissions(log.OldValues);
            baseDto.RoleName = deleted.RoleName ?? baseDto.RoleName;
            baseDto.PermissionKey = baseDto.RoleName;
            baseDto.OldValue = "defaults";
            baseDto.NewValue = "absent";
            return new List<PermissionAuditEntryDto> { baseDto };
        }

        // Override: single permission
        var ovOld = ParseOverride(log.OldValues);
        var ovNew = ParseOverride(log.NewValues);
        baseDto.PermissionKey = ovNew.Permission ?? ovOld.Permission ?? string.Empty;
        baseDto.OldValue = OverrideState(ovOld);
        baseDto.NewValue = ovNew.Removed ? "absent" : OverrideState(ovNew);
        return new List<PermissionAuditEntryDto> { baseDto };
    }

    private static string MapAction(string action) => action switch
    {
        AuditLogActions.ROLE_CREATE => "created",
        AuditLogActions.ROLE_DELETE => "deleted",
        "ROLE_PERMISSIONS_REVERT" => "reverted",
        _ => "updated",
    };

    private static string? OverrideState((string? Permission, bool? IsGranted, bool Removed) o)
    {
        if (o.Removed) return "absent";
        if (o.IsGranted == null) return null;
        return o.IsGranted.Value ? "individual" : "denied";
    }

    private static (string? RoleName, string[]? Permissions) ParseRolePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? roleName = root.TryGetProperty("roleName", out var rn) && rn.ValueKind == JsonValueKind.String
                ? rn.GetString()
                : null;
            string[]? perms = null;
            if (root.TryGetProperty("permissions", out var p) && p.ValueKind == JsonValueKind.Array)
            {
                perms = p.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
            return (roleName, perms);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (string? Permission, bool? IsGranted, bool Removed) ParseOverride(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null, false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? permission = root.TryGetProperty("permission", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : root.TryGetProperty("Permission", out var p2) && p2.ValueKind == JsonValueKind.String
                    ? p2.GetString()
                    : null;
            bool? granted = null;
            if (root.TryGetProperty("isGranted", out var g) && (g.ValueKind == JsonValueKind.True || g.ValueKind == JsonValueKind.False))
                granted = g.GetBoolean();
            else if (root.TryGetProperty("IsGranted", out var g2) && (g2.ValueKind == JsonValueKind.True || g2.ValueKind == JsonValueKind.False))
                granted = g2.GetBoolean();
            var removed = (root.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True)
                || (root.TryGetProperty("Removed", out var r2) && r2.ValueKind == JsonValueKind.True);
            return (permission, granted, removed);
        }
        catch
        {
            return (null, null, false);
        }
    }
}
