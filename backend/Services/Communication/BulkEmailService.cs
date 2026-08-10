using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Communication;

public sealed class BulkEmailService : IBulkEmailService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IBulkEmailRateLimiter _rateLimiter;
    private readonly ILogger<BulkEmailService> _logger;

    public BulkEmailService(
        AppDbContext db,
        IEmailService email,
        IBulkEmailRateLimiter rateLimiter,
        ILogger<BulkEmailService> logger)
    {
        _db = db;
        _email = email;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<BulkEmailPreviewResult> PreviewAsync(
        BulkEmailPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recipients = await ResolveRecipientsAsync(
                new BulkEmailRequest
                {
                    TenantIds = request.TenantIds,
                    FilterByLicenseType = request.FilterByLicenseType,
                    FilterByStatus = request.FilterByStatus,
                    Subject = "preview",
                    Body = "preview",
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new BulkEmailPreviewResult
        {
            RecipientCount = recipients.Count,
            TenantCount = recipients.Select(r => r.TenantId).Distinct().Count(),
        };
    }

    public async Task<BulkEmailResult> SendBulkAsync(
        BulkEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subject = request.Subject.Trim();
        var body = request.Body ?? string.Empty;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Subject and body are required.");

        var recipients = await ResolveRecipientsAsync(request, cancellationToken).ConfigureAwait(false);
        var result = new BulkEmailResult { TotalAttempted = recipients.Count };

        if (recipients.Count == 0)
            return result;

        var rateError = _rateLimiter.TryAcquireOrError(recipients.Count);
        if (rateError != null)
            throw new InvalidOperationException(rateError);

        foreach (var recipient in recipients)
        {
            var sent = await _email
                .TrySendHtmlAsync(recipient.Email, subject, body, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            _db.CommunicationLogs.Add(new CommunicationLog
            {
                Id = Guid.NewGuid(),
                TenantId = recipient.TenantId,
                Email = recipient.Email,
                Subject = subject,
                SentAt = now,
                Status = sent ? CommunicationLogStatuses.Sent : CommunicationLogStatuses.Failed,
                ErrorMessage = sent ? null : "SMTP send failed or not configured",
                CreatedAt = now,
            });

            if (sent)
            {
                result.TotalSent++;
            }
            else
            {
                result.TotalFailed++;
                result.FailedEmails.Add(recipient.Email);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Bulk email completed: attempted={Attempted} sent={Sent} failed={Failed}",
            result.TotalAttempted,
            result.TotalSent,
            result.TotalFailed);

        return result;
    }

    private async Task<List<Recipient>> ResolveRecipientsAsync(
        BulkEmailRequest request,
        CancellationToken cancellationToken)
    {
        var tenantsQuery = _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => !TenantStatuses.RemovedStatuses.Contains(t.Status));

        if (request.TenantIds is { Count: > 0 } ids)
            tenantsQuery = tenantsQuery.Where(t => ids.Contains(t.Id));

        if (request.FilterByStatus is TenantStatus status)
        {
            var storage = TenantStatuses.ToStorage(status);
            tenantsQuery = tenantsQuery.Where(t => t.Status == storage);
        }

        var tenants = await tenantsQuery
            .Select(t => new { t.Id, t.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
            return [];

        var tenantIds = tenants.Select(t => t.Id).ToList();

        if (request.FilterByLicenseType is LicenseType licenseType)
        {
            var matchingSaleTenantIds = await _db.LicenseSales.AsNoTracking().IgnoreQueryFilters()
                .Where(s =>
                    tenantIds.Contains(s.TenantId)
                    && s.Status == LicenseSaleStatuses.Active
                    && s.LicenseType == licenseType)
                .Select(s => s.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            tenantIds = matchingSaleTenantIds;
            tenants = tenants.Where(t => tenantIds.Contains(t.Id)).ToList();
            if (tenants.Count == 0)
                return [];
        }

        var managerMemberships = await (
            from m in _db.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters()
            join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
            where tenantIds.Contains(m.TenantId)
                  && m.IsActive
                  && u.IsActive
                  && u.Role == Roles.Manager
                  && u.Email != null
                  && u.Email != ""
            select new { m.TenantId, Email = u.Email! }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var byTenant = managerMemberships
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        var recipients = new List<Recipient>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tenant in tenants)
        {
            if (byTenant.TryGetValue(tenant.Id, out var emails) && emails.Count > 0)
            {
                foreach (var email in emails)
                {
                    if (seenEmails.Add(email))
                        recipients.Add(new Recipient(tenant.Id, email));
                }
            }
            else if (!string.IsNullOrWhiteSpace(tenant.Email) && seenEmails.Add(tenant.Email.Trim()))
            {
                // Fallback: tenant contact email when no Manager membership email exists.
                recipients.Add(new Recipient(tenant.Id, tenant.Email.Trim()));
            }
        }

        return recipients;
    }

    private sealed record Recipient(Guid TenantId, string Email);
}
