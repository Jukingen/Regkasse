using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

public sealed class TenantService : ITenantService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly ITenantDeletionService _tenantDeletion;
    private readonly ILogger<TenantService> _logger;

    private const string AuditEntityType = "Tenant";

    public TenantService(
        AppDbContext db,
        IAuditLogService auditLog,
        ITenantDeletionService tenantDeletion,
        ILogger<TenantService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _tenantDeletion = tenantDeletion;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == LegacyDefaultTenantIds.Primary)
            return (false, "The legacy default tenant cannot be deleted.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (false, "Tenant not found.");

        if (string.Equals(tenant.Slug, LegacyDefaultTenantIds.PrimarySlug, StringComparison.Ordinal))
            return (false, "The legacy default tenant cannot be deleted.");

        if (tenant.Status == TenantStatuses.Deleted)
            return (true, null);

        tenant.Status = TenantStatuses.Deleted;
        tenant.IsActive = false;
        tenant.DeletedAtUtc = DateTime.UtcNow;
        tenant.DeletedByUserId = actorUserId;
        tenant.UpdatedAt = tenant.DeletedAtUtc;
        tenant.UpdatedBy = actorUserId;

        var memberships = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var membership in memberships)
            membership.IsActive = false;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var affectedUserIds = memberships.Select(m => m.UserId).Distinct().ToList();
        if (affectedUserIds.Count > 0)
        {
            var users = await _db.Users
                .Where(u => affectedUserIds.Contains(u.Id) && u.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var deactivatedAt = DateTime.UtcNow;
            foreach (var user in users)
            {
                if (OperationalTenantMembershipPolicy.IsSuperAdmin(user))
                    continue;

                var hasOperational = await (
                    from m in _db.UserTenantMemberships.IgnoreQueryFilters()
                    join t in _db.Tenants on m.TenantId equals t.Id
                    where m.UserId == user.Id
                        && m.IsActive
                        && t.IsActive
                        && t.Status != TenantStatuses.Deleted
                    select m.Id
                ).AnyAsync(cancellationToken).ConfigureAwait(false);
                if (hasOperational)
                    continue;

                user.IsActive = false;
                user.DeactivatedAt = deactivatedAt;
                user.DeactivatedBy = actorUserId;
                user.DeactivationReason = $"Tenant '{tenant.Slug}' was deleted.";
                user.UpdatedAt = deactivatedAt;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Super-admin soft-deleted tenant {TenantId} slug {Slug}", tenant.Id, tenant.Slug);

        await TryLogTenantLifecycleAsync(
            AuditLogActions.TENANT_SOFT_DELETED,
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RestoreAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (false, "Tenant not found.");

        if (tenant.Status != TenantStatuses.Deleted)
            return (false, "Only deleted tenants can be restored.");

        tenant.Status = TenantStatuses.Active;
        tenant.IsActive = true;
        tenant.DeletedAtUtc = null;
        tenant.DeletedByUserId = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorUserId;

        var memberships = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && !m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var membership in memberships)
            membership.IsActive = true;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Super-admin restored tenant {TenantId} slug {Slug}", tenant.Id, tenant.Slug);

        await TryLogTenantLifecycleAsync(
            AuditLogActions.TENANT_RESTORED,
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return (true, null);
    }

    public async Task<TenantPermanentDeleteResult> HardDeleteAsync(
        Guid tenantId,
        HardDeleteAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        TenantDeleteDependenciesDto? dependencies = null;

        try
        {
            dependencies = await _tenantDeletion
                .GetDependencySummaryAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return Fail(
                "Tenant not found.",
                TenantPermanentDeleteFailureCodes.TenantNotFound);
        }

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
        {
            return Fail(
                "Tenant not found.",
                TenantPermanentDeleteFailureCodes.TenantNotFound,
                dependencies);
        }

        var confirmSlug = NormalizeSlug(request.ConfirmSlug);
        if (!string.Equals(confirmSlug, tenant.Slug, StringComparison.Ordinal))
        {
            return Fail(
                "Confirmation slug does not match tenant slug.",
                TenantPermanentDeleteFailureCodes.ConfirmSlugMismatch,
                dependencies);
        }

        var validation = await _tenantDeletion
            .ValidateHardDeleteAsync(tenantId, forceDelete: false, cancellationToken)
            .ConfigureAwait(false);
        if (!validation.Success)
        {
            return Fail(
                validation.ErrorMessage ?? "Permanent delete is not allowed for this tenant.",
                validation.ErrorCode ?? TenantPermanentDeleteFailureCodes.RemainingDependencies,
                dependencies);
        }

        if (!dependencies.CanHardDelete)
        {
            return Fail(
                dependencies.FailureMessage ?? "Permanent delete is not allowed for this tenant.",
                dependencies.FailureCode ?? TenantPermanentDeleteFailureCodes.RemainingDependencies,
                dependencies);
        }

        var tenantName = tenant.Name;
        var tenantSlug = tenant.Slug;
        var tenantNotesMarker = $"tenantId={tenantId:D}";

        try
        {
            await LogLifecycleAuditSnapshotBeforeHardDeleteAsync(
                tenantId,
                tenantName,
                tenantSlug,
                tenantNotesMarker,
                cancellationToken).ConfigureAwait(false);

            var products = await _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var categories = await _db.Categories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var settings = await _db.CompanySettings
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var memberships = await _db.UserTenantMemberships
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (products.Count > 0)
                _db.Products.RemoveRange(products);
            if (categories.Count > 0)
                _db.Categories.RemoveRange(categories);
            if (settings.Count > 0)
                _db.CompanySettings.RemoveRange(settings);
            if (memberships.Count > 0)
                _db.UserTenantMemberships.RemoveRange(memberships);

            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(
                "Super-admin permanently deleted tenant {TenantId} slug {Slug} by {ActorUserId}",
                tenantId,
                tenantSlug,
                actorUserId);

            await TryLogHardDeleteAsync(
                tenantId,
                tenantName,
                tenantSlug,
                actorUserId,
                dependencies,
                cancellationToken).ConfigureAwait(false);

            return new TenantPermanentDeleteResult(Success: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permanent tenant delete failed for {TenantId}", tenantId);
            return Fail(
                "Permanent delete failed due to remaining dependencies.",
                TenantPermanentDeleteFailureCodes.RemainingDependencies,
                dependencies);
        }
    }

    private async Task LogLifecycleAuditSnapshotBeforeHardDeleteAsync(
        Guid tenantId,
        string tenantName,
        string tenantSlug,
        string tenantNotesMarker,
        CancellationToken cancellationToken)
    {
        var lifecycleActions = new[]
        {
            AuditLogActions.TENANT_SOFT_DELETED,
            AuditLogActions.TENANT_RESTORED,
            AuditLogActions.TENANT_HARD_DELETED,
        };

        var rows = await _db.AuditLogs
            .AsNoTracking()
            .Where(a =>
                lifecycleActions.Contains(a.Action)
                && a.Notes != null
                && a.Notes.Contains(tenantNotesMarker))
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.Timestamp,
                a.Status,
                a.Notes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogWarning(
            "Permanent delete preflight for tenant {TenantId} ({Slug}): {AuditCount} lifecycle audit rows. Snapshot: {Snapshot}",
            tenantId,
            tenantSlug,
            rows.Count,
            JsonSerializer.Serialize(rows));
    }

    private async Task TryLogHardDeleteAsync(
        Guid tenantId,
        string tenantName,
        string tenantSlug,
        string? actorUserId,
        TenantDeleteDependenciesDto dependencies,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return;

        try
        {
            await _auditLog.LogSystemOperationAsync(
                AuditLogActions.TENANT_HARD_DELETED,
                AuditEntityType,
                actorUserId,
                Roles.SuperAdmin,
                description: $"{AuditLogActions.TENANT_HARD_DELETED} tenant {tenantName} ({tenantSlug})",
                notes: $"tenantId={tenantId:D};tenantName={tenantName};tenantSlug={tenantSlug}",
                status: AuditLogStatus.Success,
                requestData: new
                {
                    tenantId,
                    tenantName,
                    tenantSlug,
                    actorUserId,
                    deletedByUserId = actorUserId,
                    dependencySummary = dependencies.Dependencies,
                    deletedRecordsCount = new
                    {
                        products = dependencies.Dependencies.Products,
                        categories = dependencies.Dependencies.Categories,
                        memberships = dependencies.Dependencies.Memberships,
                    },
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for hard delete tenant {TenantId}", tenantId);
        }
    }

    private async Task TryLogTenantLifecycleAsync(
        string action,
        Guid tenantId,
        string tenantName,
        string tenantSlug,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return;

        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                actorUserId,
                Roles.SuperAdmin,
                description: $"{action} tenant {tenantName} ({tenantSlug})",
                notes: $"tenantId={tenantId:D};tenantName={tenantName};tenantSlug={tenantSlug}",
                status: AuditLogStatus.Success,
                requestData: new { tenantId, tenantName, tenantSlug, actorUserId }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for {Action} tenant {TenantId}", action, tenantId);
        }
    }

    private static TenantPermanentDeleteResult Fail(
        string message,
        string code,
        TenantDeleteDependenciesDto? dependencies = null) =>
        new(false, message, code, dependencies);

    private static string NormalizeSlug(string raw) =>
        raw.Trim().ToLowerInvariant().Replace(' ', '_');
}
