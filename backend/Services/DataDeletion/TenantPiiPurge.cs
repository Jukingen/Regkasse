using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DataDeletion;

/// <summary>
/// Removes or anonymizes personal data for one tenant. Fiscal amounts, receipt numbers,
/// and TSE signatures are left unchanged. Payment rows stay; their customer link is moved
/// to a placeholder customer so the required <c>payment_details.CustomerId</c> foreign key
/// does not block the delete (that column stays non-null because payment creation assigns it as <c>Guid</c>).
/// </summary>
internal static class TenantPiiPurge
{
    internal const string AnonymousName = "Anonymous";

    internal static async Task<Dictionary<string, int>> ExecuteAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var customerIds = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var emails = await CollectEmailsAsync(db, tenantId, ct).ConfigureAwait(false);

        counts["benefit_daily_usages"] = await DeleteWhereAsync(
            db,
            db.BenefitDailyUsages.IgnoreQueryFilters().Where(x => customerIds.Contains(x.CustomerId)),
            ct).ConfigureAwait(false);
        counts["benefit_assignments"] = await DeleteWhereAsync(
            db,
            db.BenefitAssignments.IgnoreQueryFilters().Where(x => customerIds.Contains(x.CustomerId)),
            ct).ConfigureAwait(false);

        var folioIds = await db.GuestFolios.AsNoTracking().IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        counts["guest_folio_items"] = folioIds.Count == 0
            ? 0
            : await DeleteWhereAsync(
                db,
                db.GuestFolioItems.IgnoreQueryFilters().Where(i => folioIds.Contains(i.FolioId)),
                ct).ConfigureAwait(false);
        counts["guest_folios"] = await DeleteWhereAsync(
            db,
            db.GuestFolios.IgnoreQueryFilters().Where(f => f.TenantId == tenantId),
            ct).ConfigureAwait(false);

        var registerIds = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var payments = await AnonymizePaymentsAsync(db, tenantId, registerIds, ct).ConfigureAwait(false);
        counts["payments_anonymized"] = payments.Count;
        counts["invoices_anonymized"] = await AnonymizeInvoicesAsync(db, tenantId, ct).ConfigureAwait(false);
        counts["online_orders_anonymized"] = await AnonymizeOnlineOrdersAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["orders_anonymized"] = await AnonymizeOrdersAsync(db, tenantId, ct).ConfigureAwait(false);
        counts["payment_logs_anonymized"] = await AnonymizePaymentLogsAsync(db, customerIds, ct)
            .ConfigureAwait(false);
        counts["split_items_anonymized"] = await AnonymizeSplitItemsAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["activity_events_anonymized"] = await AnonymizeActivityAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["support_tickets_anonymized"] = await AnonymizeSupportAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["communication_logs_anonymized"] = await AnonymizeCommunicationAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["operation_logs_anonymized"] = await AnonymizeOperationLogsAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["appointments_anonymized"] = await ClearAppointmentNotesAsync(db, tenantId, ct)
            .ConfigureAwait(false);
        counts["demo_requests_anonymized"] = await AnonymizeDemoRequestsAsync(db, emails, ct)
            .ConfigureAwait(false);
        counts["users_anonymized"] = await AnonymizeUsersAsync(db, tenantId, ct).ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();

        counts["audit_logs_redacted"] = await RedactAuditLogsAsync(db, tenantId, ct).ConfigureAwait(false);
        counts["elmah_redacted"] = await RedactElmahAsync(db, emails, ct).ConfigureAwait(false);

        counts["products"] = await DeleteWhereAsync(
            db, db.Products.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        counts["categories"] = await DeleteWhereAsync(
            db, db.Categories.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        var customerDelete = db.Customers.IgnoreQueryFilters().Where(x => x.TenantId == tenantId);
        if (payments.PlaceholderId.HasValue)
        {
            var keepId = payments.PlaceholderId.Value;
            customerDelete = customerDelete.Where(x => x.Id != keepId);
        }

        counts["customers"] = await DeleteWhereAsync(db, customerDelete, ct).ConfigureAwait(false);
        counts["company_settings"] = await DeleteWhereAsync(
            db, db.CompanySettings.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        counts["tenant_customizations"] = await DeleteWhereAsync(
            db, db.TenantCustomizations.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        counts["invoices_non_fiscal"] = await DeleteWhereAsync(
            db,
            db.Invoices.IgnoreQueryFilters().Where(x => x.TenantId == tenantId && x.SourcePaymentId == null),
            ct).ConfigureAwait(false);
        counts["digital_service_requests"] = await DeleteWhereAsync(
            db, db.DigitalServiceRequests.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        counts["cash_register_open_requests"] = await DeleteWhereAsync(
            db, db.CashRegisterOpenRequests.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);
        counts["monatsbeleg_auto_runs"] = await DeleteWhereAsync(
            db, db.MonatsbelegAutoRuns.IgnoreQueryFilters().Where(x => x.TenantId == tenantId), ct).ConfigureAwait(false);

        return counts;
    }

    private static async Task<HashSet<string>> CollectEmailsAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customerEmails = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.Email != "")
            .Select(c => c.Email)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var email in customerEmails)
            emails.Add(email);

        var orderEmails = await db.OnlineOrders.AsNoTracking().IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.CustomerEmail != null && o.CustomerEmail != "")
            .Select(o => o.CustomerEmail!)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var email in orderEmails)
            emails.Add(email);

        var userIds = await db.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.UserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (userIds.Count > 0)
        {
            var userEmails = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id) && u.Email != null && u.Email != "")
                .Select(u => u.Email!)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var email in userEmails)
                emails.Add(email);
        }

        return emails;
    }

    private readonly record struct PaymentAnonymization(int Count, Guid? PlaceholderId);

    private static async Task<PaymentAnonymization> AnonymizePaymentsAsync(
        AppDbContext db,
        Guid tenantId,
        List<Guid> registerIds,
        CancellationToken ct)
    {
        if (registerIds.Count == 0)
            return new PaymentAnonymization(0, null);

        var payments = await db.PaymentDetails
            .Where(p => registerIds.Contains(p.CashRegisterId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (payments.Count == 0)
            return new PaymentAnonymization(0, null);

        var placeholder = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = AnonymousName,
            Email = $"anonymous+{tenantId:N}@anonymized.invalid",
            Phone = string.Empty,
            Address = string.Empty,
            TaxNumber = string.Empty,
            Notes = string.Empty,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Customers.Add(placeholder);

        foreach (var payment in payments)
        {
            payment.CustomerId = placeholder.Id;
            payment.CustomerName = AnonymousName;
            payment.CompanyName = null;
            payment.CompanyAddress = null;
            if (!string.IsNullOrEmpty(payment.Steuernummer))
                payment.Steuernummer = AnonymousName;
        }

        return new PaymentAnonymization(payments.Count, placeholder.Id);
    }

    private static async Task<int> AnonymizeInvoicesAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var invoices = await db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var invoice in invoices)
        {
            invoice.CustomerName = AnonymousName;
            invoice.CustomerEmail = null;
            invoice.CustomerPhone = null;
            invoice.CustomerAddress = null;
            invoice.CustomerTaxNumber = null;
        }

        return invoices.Count;
    }

    private static async Task<int> AnonymizeOnlineOrdersAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var orders = await db.OnlineOrders.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var order in orders)
        {
            order.CustomerName = AnonymousName;
            order.CustomerPhone = string.Empty;
            order.CustomerEmail = null;
            order.DeliveryAddress = null;
            order.CustomerDeviceToken = null;
            order.Notes = null;
            order.CustomerId = null;
        }

        return orders.Count;
    }

    private static async Task<int> AnonymizeOrdersAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var orders = await db.Orders.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var order in orders)
        {
            order.CustomerName = AnonymousName;
            order.CustomerPhone = null;
            order.Notes = null;
        }

        return orders.Count;
    }

    private static async Task<int> AnonymizePaymentLogsAsync(
        AppDbContext db,
        List<Guid> customerIds,
        CancellationToken ct)
    {
        if (customerIds.Count == 0)
            return 0;

        var logs = await db.PaymentLogs
            .Where(p => p.CustomerId != null && customerIds.Contains(p.CustomerId.Value))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var log in logs)
        {
            log.CustomerName = AnonymousName;
            log.CustomerEmail = null;
            log.CustomerPhone = null;
            log.TaxNumber = null;
            log.Notes = null;
            log.RequestData = GdprPayloadRedactor.RedactJson(log.RequestData);
        }

        var sessions = await db.PaymentSessions
            .Where(p => p.CustomerId != null && customerIds.Contains(p.CustomerId.Value))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var session in sessions)
        {
            session.CustomerName = AnonymousName;
            session.CustomerEmail = null;
            session.CustomerPhone = null;
            session.Notes = null;
        }

        return logs.Count + sessions.Count;
    }

    private static async Task<int> AnonymizeSplitItemsAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var sessionIds = await db.SplitSessions.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (sessionIds.Count == 0)
            return 0;

        var items = await db.SplitItems
            .Where(i => sessionIds.Contains(i.SplitSessionId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var item in items)
            item.CustomerName = AnonymousName;
        return items.Count;
    }

    private static async Task<int> AnonymizeActivityAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var events = await db.ActivityEvents.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var evt in events)
        {
            evt.ActorName = AnonymousName;
            evt.Description = null;
            evt.MetadataJson = null;
        }

        return events.Count;
    }

    private static async Task<int> AnonymizeSupportAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var tickets = await db.SupportTickets.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var ticket in tickets)
        {
            ticket.CreatedByDisplayName = AnonymousName;
            ticket.AssignedToDisplayName = null;
            ticket.Message = GdprPayloadRedactor.Redacted;
            ticket.Title = GdprPayloadRedactor.Redacted;
        }

        var messages = await db.SupportTicketMessages.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var message in messages)
        {
            message.AuthorDisplayName = AnonymousName;
            message.Body = GdprPayloadRedactor.Redacted;
        }

        return tickets.Count + messages.Count;
    }

    private static async Task<int> AnonymizeCommunicationAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var rows = await db.CommunicationLogs.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.Email = $"redacted+{row.Id:N}@anonymized.invalid";
            row.Subject = GdprPayloadRedactor.Redacted;
            row.ErrorMessage = null;
        }

        return rows.Count;
    }

    private static async Task<int> AnonymizeOperationLogsAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var rows = await db.OperationLogs.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.BeforeState = GdprPayloadRedactor.RedactJson(row.BeforeState);
            row.AfterState = GdprPayloadRedactor.RedactJson(row.AfterState);
            row.Reason = null;
            row.IpAddress = null;
            row.UserAgent = null;
        }

        return rows.Count;
    }

    private static async Task<int> ClearAppointmentNotesAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var rows = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Notes != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
            row.Notes = null;
        return rows.Count;
    }

    private static async Task<int> AnonymizeDemoRequestsAsync(
        AppDbContext db,
        HashSet<string> emails,
        CancellationToken ct)
    {
        if (emails.Count == 0)
            return 0;

        var rows = await db.DemoRequests
            .Where(d => emails.Contains(d.Email))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            row.Name = AnonymousName;
            row.Email = $"redacted+{row.Id:N}@anonymized.invalid";
            row.Company = null;
            row.Message = null;
        }

        return rows.Count;
    }

    private static async Task<int> AnonymizeUsersAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var memberships = await db.UserTenantMemberships.IgnoreQueryFilters()
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

        var userIds = await db.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
            return 0;

        var superAdminIds = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.SuperAdmin
            select userRole.UserId
        ).ToListAsync(ct).ConfigureAwait(false);
        var superAdmins = superAdminIds.ToHashSet(StringComparer.Ordinal);

        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var anonymized = 0;
        var now = DateTime.UtcNow;
        foreach (var user in users)
        {
            if (superAdmins.Contains(user.Id))
                continue;

            var hasOtherTenant = await (
                from membership in db.UserTenantMemberships.IgnoreQueryFilters()
                join tenant in db.Tenants.IgnoreQueryFilters() on membership.TenantId equals tenant.Id
                where membership.UserId == user.Id
                    && membership.IsActive
                    && tenant.IsActive
                    && !TenantStatuses.RemovedStatuses.Contains(tenant.Status)
                select membership.Id
            ).AnyAsync(ct).ConfigureAwait(false);
            if (hasOtherTenant)
                continue;

            var local = AnonymizedLocalPart(user.Id);
            var email = $"{local}@anonymized.invalid";
            user.Email = email;
            user.NormalizedEmail = email.ToUpperInvariant();
            user.UserName = $"anon-{local}";
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.FirstName = AnonymousName;
            user.LastName = "User";
            user.PhoneNumber = null;
            user.TaxNumber = null;
            user.Notes = string.Empty;
            user.EmployeeNumber = string.Empty;
            user.IsActive = false;
            user.DeactivatedAt ??= now;
            user.DeactivationReason = "Tenant customer data purged after expired license.";
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = now;
            anonymized++;
        }

        return anonymized;
    }

    internal static string AnonymizedLocalPart(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static async Task<int> RedactAuditLogsAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var logs = await db.AuditLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .Select(a => new
            {
                a.Id,
                a.OldValues,
                a.NewValues,
                a.RequestData,
                a.ResponseData,
                a.Description,
                a.Notes,
                a.EntityName,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var updated = 0;
        foreach (var log in logs)
        {
            var oldValues = GdprPayloadRedactor.RedactJson(log.OldValues);
            var newValues = GdprPayloadRedactor.RedactJson(log.NewValues);
            var requestData = GdprPayloadRedactor.RedactJson(log.RequestData);
            var responseData = GdprPayloadRedactor.RedactJson(log.ResponseData);
            var description = GdprPayloadRedactor.RedactFreeText(log.Description);
            var notes = GdprPayloadRedactor.RedactFreeText(log.Notes);
            var entityName = GdprPayloadRedactor.RedactFreeText(log.EntityName);
            if (oldValues == log.OldValues
                && newValues == log.NewValues
                && requestData == log.RequestData
                && responseData == log.ResponseData
                && description == log.Description
                && notes == log.Notes
                && entityName == log.EntityName)
            {
                continue;
            }

            if (db.Database.IsRelational())
            {
                await db.AuditLogs.IgnoreQueryFilters()
                    .Where(a => a.Id == log.Id)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.OldValues, oldValues)
                            .SetProperty(a => a.NewValues, newValues)
                            .SetProperty(a => a.RequestData, requestData)
                            .SetProperty(a => a.ResponseData, responseData)
                            .SetProperty(a => a.Description, description)
                            .SetProperty(a => a.Notes, notes)
                            .SetProperty(a => a.EntityName, entityName),
                        ct)
                    .ConfigureAwait(false);
            }
            else
            {
                // In-memory tests have no ExecuteUpdate. Apply the same field writes without
                // marking the append-only audit row Modified (SaveChanges would reject that).
                var entity = await db.AuditLogs.IgnoreQueryFilters()
                    .FirstAsync(a => a.Id == log.Id, ct)
                    .ConfigureAwait(false);
                entity.OldValues = oldValues;
                entity.NewValues = newValues;
                entity.RequestData = requestData;
                entity.ResponseData = responseData;
                entity.Description = description;
                entity.Notes = notes;
                entity.EntityName = entityName;
                // Keep the redacted values on the tracked instance. Setting State = Unchanged
                // directly would copy the store snapshot back over them, and SaveChanges
                // rejects Modified audit rows (append-only).
                var entry = db.Entry(entity);
                foreach (var property in entry.Properties)
                    property.OriginalValue = property.CurrentValue;
                entry.State = EntityState.Unchanged;
            }

            updated++;
        }

        return updated;
    }

    private static async Task<int> RedactElmahAsync(
        AppDbContext db,
        HashSet<string> emails,
        CancellationToken ct)
    {
        if (!db.Database.IsRelational() || emails.Count == 0)
            return 0;

        var updated = 0;
        var transaction = db.Database.CurrentTransaction;
        foreach (var email in emails)
        {
            var needle = email.Replace("%", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal);
            if (needle.Length < 3)
                continue;

            var pattern = "%" + needle + "%";
            try
            {
                if (transaction != null)
                    await transaction.CreateSavepointAsync("elmah_pii", ct).ConfigureAwait(false);

                updated += await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE elmah_error
                    SET "User" = 'anonymous',
                        "Message" = '[redacted]',
                        "AllXml" = '[redacted]'
                    WHERE "User" ILIKE {pattern}
                       OR "Message" ILIKE {pattern}
                       OR "AllXml" ILIKE {pattern}
                    """,
                    ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackToSavepointAsync("elmah_pii", ct).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The provider has no savepoints. Elmah redaction must not undo the purge.
                    }
                }
            }
        }

        return updated;
    }

    /// <summary>
    /// PostgreSQL uses set-based <see cref="RelationalQueryableExtensions.ExecuteDeleteAsync{TSource}"/>
    /// inside the caller transaction. The in-memory provider has no bulk delete, so tests remove rows
    /// through the change tracker and save in that same transaction.
    /// </summary>
    private static async Task<int> DeleteWhereAsync<T>(
        AppDbContext db,
        IQueryable<T> query,
        CancellationToken ct)
        where T : class
    {
        if (db.Database.IsRelational())
            return await query.ExecuteDeleteAsync(ct).ConfigureAwait(false);

        var rows = await query.ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0)
            return 0;

        db.Set<T>().RemoveRange(rows);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return rows.Count;
    }
}
