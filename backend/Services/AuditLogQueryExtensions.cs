using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

internal static class AuditLogQueryExtensions
{
    public static IQueryable<AuditLog> ApplyFilters(this IQueryable<AuditLog> query, AuditLogQueryFilters filters)
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
                a.UserRole != Roles.SuperAdmin
                && a.Action != AuditLogActions.TENANT_IMPERSONATION_STARTED
                && a.Action != AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD
                && a.Action != AuditLogActions.TENANT_SOFT_DELETED
                && a.Action != AuditLogActions.TENANT_HARD_DELETED
                && a.Action != AuditLogActions.TENANT_RESTORED);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = $"%{filters.Search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Action, term)
                || (a.Description != null && EF.Functions.ILike(a.Description, term))
                || EF.Functions.ILike(a.EntityType, term)
                || (a.EntityName != null && EF.Functions.ILike(a.EntityName, term))
                || (a.IpAddress != null && EF.Functions.ILike(a.IpAddress, term)));
        }

        return query;
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
