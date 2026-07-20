using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.DataDeletion;

/// <summary>
/// Irreversible non-RKSV customer-data purge for Archived tenants after confirmation + 7-day wait.
/// Keeps fiscal/RKSV rows, online orders, vouchers; soft-removes memberships and deactivates users.
/// </summary>
public sealed class DataDeletionService : IDataDeletionService
{
    public const int ConfirmationWaitDays = 7;
    public const int RksvRetentionYears = 7;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDataDeletionNotificationSender _notifications;
    private readonly IAuditLogService _audit;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<EmailSmtpOptions> _emailOptions;
    private readonly ILogger<DataDeletionService> _logger;

    public DataDeletionService(
        IDbContextFactory<AppDbContext> dbFactory,
        IDataDeletionNotificationSender notifications,
        IAuditLogService audit,
        UserManager<ApplicationUser> userManager,
        IOptions<EmailSmtpOptions> emailOptions,
        ILogger<DataDeletionService> logger)
    {
        _dbFactory = dbFactory;
        _notifications = notifications;
        _audit = audit;
        _userManager = userManager;
        _emailOptions = emailOptions;
        _logger = logger;
    }

    public async Task<TenantDataDeletionRequestDto> RequestDeletionAsync(
        Guid tenantId,
        string? requestedByUserId,
        string? reason,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (tenant.CustomerDataPurgedAtUtc.HasValue)
            throw new InvalidOperationException("Customer data was already purged for this tenant.");

        var lifecycleDay = new TenantLicenseValidator().GetStatus(tenant.LicenseValidUntilUtc);
        if (lifecycleDay != TenantLicenseStatus.Archived)
        {
            throw new InvalidOperationException(
                "Data deletion requires an Archived license (more than 30 days overdue).");
        }

        var existing = await db.TenantDataDeletionRequests
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.Status != TenantDataDeletionRequestStatuses.Cancelled
                        && r.Status != TenantDataDeletionRequestStatuses.Completed)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing != null)
            return Map(existing);

        var row = new TenantDataDeletionRequest
        {
            TenantId = tenantId,
            Status = TenantDataDeletionRequestStatuses.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = DateTime.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        db.TenantDataDeletionRequests.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await TryNotifyRequestAsync(db, tenant, row, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant data deletion requested. TenantId={TenantId}, RequestId={RequestId}",
            tenantId,
            row.Id);

        return Map(row);
    }

    public async Task<TenantDataDeletionRequestDto> ConfirmDeletionAsync(
        Guid tenantId,
        Guid requestId,
        string? confirmedByUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var request = await db.TenantDataDeletionRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Deletion request not found.");

        if (request.Status == TenantDataDeletionRequestStatuses.Completed)
            throw new InvalidOperationException("Deletion already completed.");

        if (request.Status == TenantDataDeletionRequestStatuses.Cancelled)
            throw new InvalidOperationException("Deletion request was cancelled.");

        if (request.Status == TenantDataDeletionRequestStatuses.Confirmed
            && request.ConfirmedAtUtc.HasValue)
            return Map(request);

        if (request.Status is not (TenantDataDeletionRequestStatuses.Pending
            or TenantDataDeletionRequestStatuses.ExportReady))
        {
            throw new InvalidOperationException($"Cannot confirm deletion in status '{request.Status}'.");
        }

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        request.Status = TenantDataDeletionRequestStatuses.Confirmed;
        request.ConfirmedAtUtc = DateTime.UtcNow;
        request.ConfirmedByUserId = confirmedByUserId;
        request.UpdatedAt = DateTime.UtcNow;

        var linkedRights = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .Where(r => r.LinkedDeletionRequestId == request.Id
                        && r.Status != TenantDataRightsRequestStatuses.Completed
                        && r.Status != TenantDataRightsRequestStatuses.Cancelled)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var purgeEligible = request.ConfirmedAtUtc.Value.AddDays(ConfirmationWaitDays);
        foreach (var rights in linkedRights)
        {
            rights.Status = TenantDataRightsRequestStatuses.Confirmed;
            rights.ProcessingDeadlineUtc = purgeEligible;
            rights.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await TryNotifyConfirmedAsync(db, tenant, request, ct).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "TENANT_DATA_DELETION_CONFIRMED",
            entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
            userId: confirmedByUserId ?? "unknown",
            userRole: "Unknown",
            description: $"Data deletion confirmed; purge eligible after {ConfirmationWaitDays} days.",
            status: AuditLogStatus.Success,
            tenantId: tenantId).ConfigureAwait(false);

        return Map(request);
    }

    public async Task<DeletionResult> ExecutePurgeAsync(
        Guid requestId,
        string? actorUserId,
        string executedVia,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var request = await db.TenantDataDeletionRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            .ConfigureAwait(false);

        if (request == null)
            return DeletionResult.Fail("Request not found", DataDeletionErrorCodes.NotFound);

        if (request.Status == TenantDataDeletionRequestStatuses.Completed)
            return DeletionResult.Fail("Deletion already completed", DataDeletionErrorCodes.AlreadyCompleted);

        if (request.Status == TenantDataDeletionRequestStatuses.Cancelled)
            return DeletionResult.Fail("Deletion request was cancelled", DataDeletionErrorCodes.InvalidStatus);

        if (request.Status != TenantDataDeletionRequestStatuses.Confirmed
            || !request.ConfirmedAtUtc.HasValue)
        {
            return DeletionResult.Fail(
                "Deletion must be confirmed in FA before purge.",
                DataDeletionErrorCodes.NotConfirmed);
        }

        var waitEnds = request.ConfirmedAtUtc.Value.AddDays(ConfirmationWaitDays);
        if (waitEnds > DateTime.UtcNow)
        {
            return DeletionResult.Fail(
                $"Grace period not yet completed. Eligible at {waitEnds:O} UTC.",
                DataDeletionErrorCodes.GracePeriodActive);
        }

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
            .ConfigureAwait(false);

        if (tenant == null)
            return DeletionResult.Fail("Tenant not found", DataDeletionErrorCodes.NotFound);

        if (tenant.CustomerDataPurgedAtUtc.HasValue)
            return DeletionResult.Fail("Customer data was already purged", DataDeletionErrorCodes.AlreadyPurged);

        var dayStatus = new TenantLicenseValidator().GetStatus(tenant.LicenseValidUntilUtc);
        if (dayStatus != TenantLicenseStatus.Archived)
        {
            return DeletionResult.Fail(
                "Tenant license is not Archived (requires more than 30 days overdue).",
                DataDeletionErrorCodes.NotArchived);
        }

        var counts = await DeleteNonRksvDataAsync(db, request.TenantId, ct).ConfigureAwait(false);
        await SoftRemoveUserAccountsAsync(db, request.TenantId, actorUserId, ct).ConfigureAwait(false);

        tenant.CustomerDataPurgedAtUtc = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorUserId;

        request.Status = TenantDataDeletionRequestStatuses.Completed;
        request.CompletedAtUtc = DateTime.UtcNow;
        request.CompletedByUserId = actorUserId;
        request.ExecutedVia = executedVia;
        request.UpdatedAt = DateTime.UtcNow;

        // Keep GDPR data-rights Delete requests in sync (including AutoPurge).
        var linkedRights = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .Where(r => r.LinkedDeletionRequestId == request.Id
                        && r.Status != TenantDataRightsRequestStatuses.Completed
                        && r.Status != TenantDataRightsRequestStatuses.Cancelled)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var rights in linkedRights)
        {
            rights.Status = TenantDataRightsRequestStatuses.Completed;
            rights.CompletedAtUtc = DateTime.UtcNow;
            rights.CompletedByUserId = actorUserId;
            rights.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "TENANT_DATA_PURGED",
            entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
            userId: actorUserId ?? "system",
            userRole: executedVia == TenantDataDeletionExecutedVia.Auto ? "System" : "SuperAdmin",
            description:
                $"Non-RKSV customer data purged (irreversible). RKSV retention {RksvRetentionYears} years. Via={executedVia}.",
            status: AuditLogStatus.Success,
            responseData: counts,
            tenantId: request.TenantId).ConfigureAwait(false);

        _logger.LogWarning(
            "Tenant customer data purged. TenantId={TenantId}, RequestId={RequestId}, Via={Via}, Counts={@Counts}",
            request.TenantId,
            request.Id,
            executedVia,
            counts);

        return DeletionResult.Success(request.Id, request.TenantId, counts);
    }

    public async Task<IReadOnlyList<Guid>> ListPurgeEligibleRequestIdsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var cutoff = DateTime.UtcNow.AddDays(-ConfirmationWaitDays);

        return await db.TenantDataDeletionRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Status == TenantDataDeletionRequestStatuses.Confirmed
                        && r.ConfirmedAtUtc != null
                        && r.ConfirmedAtUtc <= cutoff)
            .Select(r => r.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes purgeable non-RKSV rows. Never touches payments, receipts, fiscal invoices,
    /// audit logs, TSE signatures, online orders, or vouchers.
    /// </summary>
    internal static async Task<Dictionary<string, int>> DeleteNonRksvDataAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["products"] = await db.Products.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["categories"] = await db.Categories.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["customers"] = await db.Customers.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["company_settings"] = await db.CompanySettings.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["tenant_customizations"] = await db.TenantCustomizations.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["invoices_non_fiscal"] = await db.Invoices.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.SourcePaymentId == null)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false),
            ["digital_service_requests"] = await db.DigitalServiceRequests.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct).ConfigureAwait(false),
        };

        return counts;
    }

    private async Task SoftRemoveUserAccountsAsync(
        AppDbContext db,
        Guid tenantId,
        string? actorUserId,
        CancellationToken ct)
    {
        var memberships = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var membership in memberships)
        {
            membership.IsActive = false;
            membership.IsOwner = false;
            membership.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var affectedUserIds = memberships.Select(m => m.UserId).Distinct().ToList();
        if (affectedUserIds.Count == 0)
            return;

        var users = await db.Users
            .Where(u => affectedUserIds.Contains(u.Id) && u.IsActive)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var deactivatedAt = DateTime.UtcNow;
        foreach (var user in users)
        {
            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin).ConfigureAwait(false))
                continue;

            var hasOperational = await (
                from m in db.UserTenantMemberships.IgnoreQueryFilters()
                join t in db.Tenants.IgnoreQueryFilters() on m.TenantId equals t.Id
                where m.UserId == user.Id
                    && m.IsActive
                    && t.IsActive
                    && t.Status != TenantStatuses.Deleted
                select m.Id
            ).AnyAsync(ct).ConfigureAwait(false);

            if (hasOperational)
                continue;

            user.IsActive = false;
            user.DeactivatedAt = deactivatedAt;
            user.DeactivatedBy = actorUserId;
            user.DeactivationReason = "Tenant customer data purged after expired license.";
            user.UpdatedAt = deactivatedAt;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task TryNotifyRequestAsync(
        AppDbContext db,
        Tenant tenant,
        TenantDataDeletionRequest request,
        CancellationToken ct)
    {
        try
        {
            var (to, cc) = await ResolveRecipientsAsync(db, tenant.Id, ct).ConfigureAwait(false);
            var eligibleAt = request.RequestedAtUtc.AddDays(ConfirmationWaitDays);
            var body =
                $"Data deletion was requested for tenant '{tenant.Name}' ({tenant.Slug}).\n\n" +
                $"RequestId: {request.Id}\n" +
                $"Reason: {request.Reason ?? "(none)"}\n\n" +
                "Please confirm the deletion in the Admin panel (Datenverwaltung).\n" +
                $"After confirmation, a {ConfirmationWaitDays}-day wait applies before irreversible purge of non-RKSV data.\n" +
                "RKSV fiscal data (payments, receipts, fiscal invoices, audit, TSE) is retained for 7 years.\n" +
                "Online orders and vouchers are retained.\n";

            await _notifications.SendAsync(
                to,
                cc,
                subject: $"[Regkasse] Data deletion requested — {tenant.Slug}",
                plainBody: body,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data deletion request email failed for tenant {TenantId}", tenant.Id);
        }
    }

    private async Task TryNotifyConfirmedAsync(
        AppDbContext db,
        Tenant tenant,
        TenantDataDeletionRequest request,
        CancellationToken ct)
    {
        try
        {
            var (to, cc) = await ResolveRecipientsAsync(db, tenant.Id, ct).ConfigureAwait(false);
            var eligibleAt = request.ConfirmedAtUtc!.Value.AddDays(ConfirmationWaitDays);
            var body =
                $"Data deletion was confirmed for tenant '{tenant.Name}' ({tenant.Slug}).\n\n" +
                $"RequestId: {request.Id}\n" +
                $"ConfirmedAtUtc: {request.ConfirmedAtUtc:O}\n" +
                $"PurgeEligibleAtUtc: {eligibleAt:O}\n\n" +
                "Non-RKSV data will be purged automatically after the wait, or a Super Admin may execute manually.\n" +
                "This purge is irreversible (restore from backup only).\n";

            await _notifications.SendAsync(
                to,
                cc,
                subject: $"[Regkasse] Data deletion confirmed — {tenant.Slug}",
                plainBody: body,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data deletion confirmation email failed for tenant {TenantId}", tenant.Id);
        }
    }

    private async Task<(IReadOnlyList<string> To, IReadOnlyList<string> Cc)> ResolveRecipientsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var managerEmails = await (
            from m in db.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.TenantId == tenantId && m.IsActive && u.Email != null
            select new { u.Id, u.Email, m.IsOwner }
        ).ToListAsync(ct).ConfigureAwait(false);

        var to = new List<string>();
        foreach (var row in managerEmails.OrderByDescending(x => x.IsOwner))
        {
            var user = await _userManager.FindByIdAsync(row.Id).ConfigureAwait(false);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                continue;
            if (await _userManager.IsInRoleAsync(user, Roles.Manager).ConfigureAwait(false)
                || row.IsOwner)
            {
                to.Add(user.Email.Trim());
            }
        }

        to = to.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var cc = new List<string>();
        var reminderCc = _emailOptions.Value.LicenseReminderRecipients;
        if (!string.IsNullOrWhiteSpace(reminderCc))
        {
            foreach (var part in reminderCc.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Contains('@', StringComparison.Ordinal))
                    cc.Add(part);
            }
        }

        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
        foreach (var sa in superAdmins)
        {
            if (!string.IsNullOrWhiteSpace(sa.Email))
                cc.Add(sa.Email.Trim());
        }

        cc = cc.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(e => !to.Contains(e, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (to.Count == 0 && cc.Count > 0)
        {
            to.Add(cc[0]);
            cc.RemoveAt(0);
        }

        return (to, cc);
    }

    internal static TenantDataDeletionRequestDto Map(TenantDataDeletionRequest row)
    {
        DateTime? eligibleAt = row.ConfirmedAtUtc?.AddDays(ConfirmationWaitDays);
        return new TenantDataDeletionRequestDto
        {
            Id = row.Id,
            Status = row.Status,
            Reason = row.Reason,
            RequestedAtUtc = row.RequestedAtUtc,
            ExportCompletedAtUtc = row.ExportCompletedAtUtc,
            ConfirmedAtUtc = row.ConfirmedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            PurgeEligibleAtUtc = eligibleAt,
            ExecutedVia = row.ExecutedVia,
            ConfirmationWaitDays = ConfirmationWaitDays,
        };
    }
}
