using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Stored full-text expression. Underscores become spaces so an action such as USER_LOGIN matches "login".
/// </summary>
internal static class AuditLogSearchVector
{
    public const string StoredExpression =
        "to_tsvector('simple', replace(replace(replace(coalesce(action, '') || ' ' || coalesce(entity_type, '') || ' ' || coalesce(description, '') || ' ' || coalesce(user_email, ''), '_', ' '), '@', ' '), '.', ' '))";
}

internal static class AuditLogQueryExtensions
{
    public static IQueryable<AuditLog> ApplyFilters(
        this IQueryable<AuditLog> query,
        AuditLogQueryFilters filters,
        AppDbContext? db = null)
    {
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(filters.StartDate, filters.EndDate);
        if (lo.HasValue)
            query = query.Where(a => a.Timestamp >= lo.Value);
        if (hi.HasValue)
            query = query.Where(a => a.Timestamp < hi.Value);

        if (!string.IsNullOrWhiteSpace(filters.UserId))
            query = query.Where(a => a.UserId == filters.UserId.Trim());

        if (!string.IsNullOrWhiteSpace(filters.UserRole))
            query = query.Where(a => a.UserRole == filters.UserRole.Trim());

        if (!string.IsNullOrWhiteSpace(filters.TargetUserId))
        {
            var target = filters.TargetUserId.Trim();
            query = query.Where(a =>
                a.EntityName == target
                || (a.Metadata != null && EF.Functions.ILike(a.Metadata, $"%\"targetUserId\":\"{target}\"%"))
                || (a.Metadata != null && EF.Functions.ILike(a.Metadata, $"%\"targetUserId\": \"{target}\"%")));
        }

        if (!string.IsNullOrWhiteSpace(filters.Action))
            query = query.Where(a => a.Action == filters.Action.Trim());

        if (!string.IsNullOrWhiteSpace(filters.EntityType))
            query = query.Where(a => a.EntityType == filters.EntityType.Trim());

        if (filters.EntityId.HasValue)
            query = query.Where(a => a.EntityId == filters.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(filters.IpAddress))
        {
            var ip = filters.IpAddress.Trim();
            query = query.Where(a => a.IpAddress != null && a.IpAddress == ip);
        }

        if (filters.Status.HasValue)
        {
            query = query.Where(a => a.Status == filters.Status.Value);
        }
        else if (!string.IsNullOrWhiteSpace(filters.StatusOutcome))
        {
            var outcome = filters.StatusOutcome.Trim().ToLowerInvariant();
            if (outcome == "success")
                query = query.Where(a => a.Status == AuditLogStatus.Success);
            else if (outcome == "failure")
                query = query.Where(a => a.Status != AuditLogStatus.Success);
        }

        if (filters.HasChanges == true)
        {
            query = query.Where(a =>
                (a.OldValues != null && a.OldValues != "")
                || (a.NewValues != null && a.NewValues != "")
                || (a.Changes != null && a.Changes != ""));
        }
        else if (filters.HasChanges == false)
        {
            query = query.Where(a =>
                (a.OldValues == null || a.OldValues == "")
                && (a.NewValues == null || a.NewValues == "")
                && (a.Changes == null || a.Changes == ""));
        }

        if (filters.ExcludePlatformOperatorActors)
        {
            query = query.Where(a =>
                a.UserRole.ToLower() != Roles.SuperAdmin.ToLower()
                && a.UserRole.ToLower() != RoleHierarchy.PlatformAdmin.ToLower()
                && a.Action != AuditLogActions.TENANT_IMPERSONATION_STARTED
                && a.Action != AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD
                && a.Action != AuditLogActions.TENANT_SOFT_DELETED
                && a.Action != AuditLogActions.TENANT_HARD_DELETED
                && a.Action != AuditLogActions.TENANT_RESTORED);
            if (db != null)
                query = ApplyTenantStaffActorScope(query, db);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim().Replace('_', ' ').Replace('@', ' ').Replace('.', ' ');
            query = query.Where(a =>
                a.SearchVector!.Matches(EF.Functions.PlainToTsQuery("simple", term)));
        }

        return query;
    }

    /// <summary>
    /// Tenant-admin view: drop platform operators and users who have an account but no active membership on the row's tenant.
    /// Rows whose user id is not in AspNetUsers stay (system / deleted actors) when the stored role is not a platform role.
    /// </summary>
    private static IQueryable<AuditLog> ApplyTenantStaffActorScope(IQueryable<AuditLog> query, AppDbContext db)
    {
        var superAdmin = Roles.SuperAdmin.ToLowerInvariant();
        var platformAdmin = RoleHierarchy.PlatformAdmin.ToLowerInvariant();
        return query.Where(a =>
            !db.Users.Any(u =>
                u.Id == a.UserId
                && u.Role != null
                && (u.Role.ToLower() == superAdmin || u.Role.ToLower() == platformAdmin))
            && (
                !db.Users.Any(u => u.Id == a.UserId)
                || db.UserTenantMemberships.Any(m =>
                    m.UserId == a.UserId && m.TenantId == a.TenantId && m.IsActive)));
    }

    public static AuditLogQueryFilters ToFilters(
        DateTime? startDate,
        DateTime? endDate,
        string? userId,
        string? userRole,
        string? targetUserId,
        string? action,
        string? entityType,
        Guid? entityId,
        string? ipAddress,
        AuditLogStatus? status,
        string? statusOutcome,
        bool? hasChanges) =>
        new()
        {
            StartDate = startDate,
            EndDate = endDate,
            UserId = userId,
            UserRole = userRole,
            TargetUserId = targetUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            Status = status,
            StatusOutcome = statusOutcome,
            HasChanges = hasChanges,
        };
}
