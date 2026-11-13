using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Onboarding;

public sealed class TenantOnboardingChecklistService : ITenantOnboardingChecklistService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<TenantOnboardingChecklistService> _logger;

    public TenantOnboardingChecklistService(
        AppDbContext db,
        IEmailService email,
        ILogger<TenantOnboardingChecklistService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<TenantOnboardingOverviewDto> EnsureAndGetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStepsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await BuildOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TenantOnboardingOverviewDto> CompleteStepAsync(
        Guid tenantId,
        string step,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(step)
            || !TenantOnboardingSteps.DefaultOrder.Contains(step, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown onboarding step '{step}'.");
        }

        await EnsureStepsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var normalized = TenantOnboardingSteps.DefaultOrder
            .First(s => s.Equals(step, StringComparison.OrdinalIgnoreCase));

        var row = await _db.TenantOnboardingStatuses
            .IgnoreQueryFilters()
            .FirstAsync(s => s.TenantId == tenantId && s.Step == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (!row.IsCompleted)
        {
            row.IsCompleted = true;
            row.CompletedAtUtc = DateTime.UtcNow;
            row.CompletedByUserId = actorUserId;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await NotifyStepCompletedAsync(tenantId, normalized, cancellationToken).ConfigureAwait(false);
        }

        return await BuildOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureStepsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.TenantOnboardingStatuses.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Step)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        foreach (var step in TenantOnboardingSteps.DefaultOrder)
        {
            if (existing.Contains(step, StringComparer.Ordinal))
                continue;

            var isAccountCreated = step == TenantOnboardingSteps.AccountCreated;
            _db.TenantOnboardingStatuses.Add(new TenantOnboardingStatus
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Step = step,
                IsCompleted = isAccountCreated,
                CompletedAtUtc = isAccountCreated ? now : null,
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TenantOnboardingOverviewDto> BuildOverviewAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.TenantOnboardingStatuses.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var steps = TenantOnboardingSteps.DefaultOrder
            .Select(step =>
            {
                var row = rows.FirstOrDefault(r => r.Step == step);
                return new OnboardingStepDto
                {
                    Step = step,
                    IsCompleted = row?.IsCompleted ?? false,
                    CompletedAtUtc = row?.CompletedAtUtc,
                };
            })
            .ToList();

        var completed = steps.Count(s => s.IsCompleted);
        return new TenantOnboardingOverviewDto
        {
            TenantId = tenantId,
            CompletedCount = completed,
            TotalCount = steps.Count,
            IsFullyComplete = completed == steps.Count,
            Steps = steps,
        };
    }

    private async Task NotifyStepCompletedAsync(
        Guid tenantId,
        string step,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (tenant == null)
                return;

            var managerEmail = await (
                    from m in _db.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters()
                    join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
                    where m.TenantId == tenantId
                          && m.IsActive
                          && u.IsActive
                          && u.Role == Roles.Manager
                          && u.Email != null
                          && u.Email != ""
                    select u.Email)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var to = managerEmail ?? tenant.Email;
            if (string.IsNullOrWhiteSpace(to))
                return;

            var subject = $"Regkasse Onboarding: {step} abgeschlossen";
            var body =
                $"<p>Hallo,</p><p>Der Onboarding-Schritt <strong>{step}</strong> für Mandant <strong>{tenant.Name}</strong> wurde abgeschlossen.</p>";
            await _email.TrySendHtmlAsync(to, subject, body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send onboarding step email for tenant {TenantId}", tenantId);
        }
    }
}
