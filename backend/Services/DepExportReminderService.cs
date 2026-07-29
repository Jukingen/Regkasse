using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IDepExportReminderService
{
    /// <summary>
    /// Scans active tenants and publishes activity reminders for incomplete DEP requirements
    /// at 30 / 7 / 1 day and overdue milestones.
    /// </summary>
    Task<DepExportReminderSweepResult> CheckAndNotifyAsync(CancellationToken cancellationToken = default);
}

public sealed record DepExportReminderSweepResult(int TenantsScanned, int RemindersSent, int Skipped);

/// <summary>
/// Publishes DEP export compliance reminders into the activity feed (FA bell + configured email/webhook)
/// and optional mobile push via <see cref="IDepExportPushNotificationService"/>.
/// Distinct from cron automation <see cref="DepExportScheduler"/>.
/// </summary>
public sealed class DepExportReminderService : IDepExportReminderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DepExportReminderOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DepExportReminderService> _logger;

    public DepExportReminderService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DepExportReminderOptions> options,
        TimeProvider timeProvider,
        ILogger<DepExportReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DepExportReminderSweepResult> CheckAndNotifyAsync(
        CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return new DepExportReminderSweepResult(0, 0, 0);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var tenantAccessor = sp.GetRequiredService<ICurrentTenantAccessor>();
        tenantAccessor.TenantId = null;

        var db = sp.GetRequiredService<AppDbContext>();
        var requirementService = sp.GetRequiredService<IDepExportRequirementService>();
        var activity = sp.GetRequiredService<IActivityEventService>();
        var push = sp.GetRequiredService<IDepExportPushNotificationService>();

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sent = 0;
        var skipped = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                tenantAccessor.TenantId = tenant.Id;
                var (tenantSent, tenantSkipped) = await NotifyTenantAsync(
                        db,
                        requirementService,
                        activity,
                        push,
                        tenant.Id,
                        tenant.Name,
                        opt.LegalOnly,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);
                sent += tenantSent;
                skipped += tenantSkipped;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "DEP export reminder sweep failed for tenant {TenantId}",
                    tenant.Id);
            }
            finally
            {
                tenantAccessor.TenantId = null;
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "DEP export reminders published: tenants={Tenants} sent={Sent} skipped={Skipped}",
                tenants.Count,
                sent,
                skipped);
        }

        return new DepExportReminderSweepResult(tenants.Count, sent, skipped);
    }

    private async Task<(int Sent, int Skipped)> NotifyTenantAsync(
        AppDbContext db,
        IDepExportRequirementService requirementService,
        IActivityEventService activity,
        IDepExportPushNotificationService push,
        Guid tenantId,
        string? tenantName,
        bool legalOnly,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var requirements = await requirementService
            .GetRequirementsAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;

        foreach (var requirement in requirements.Where(r => !r.IsCompleted && r.DueDate.HasValue))
        {
            if (legalOnly && requirement.RequirementType != DepExportRequirementTypes.Legal)
            {
                skipped++;
                continue;
            }

            // Skip duplicate Urgent row when Yearly Legal already covered the same period.
            if (requirement.Category == DepExportRequirementCategories.Urgent)
            {
                skipped++;
                continue;
            }

            var daysUntilDue = DepExportReminderMilestones.DaysUntilDue(requirement.DueDate!.Value, utcNow);
            var milestone = DepExportReminderMilestones.ResolveMilestone(daysUntilDue);
            if (milestone is null)
            {
                skipped++;
                continue;
            }

            var dedupKey = DepExportReminderMilestones.BuildDedupKey(tenantId, requirement, milestone, utcNow);
            var alreadySent = await db.ActivityEvents
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    e => e.TenantId == tenantId && e.DedupKey == dedupKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (alreadySent)
            {
                skipped++;
                continue;
            }

            var title = DepExportReminderMilestones.BuildTitle(milestone);
            var description = DepExportReminderMilestones.BuildGermanMessage(milestone, requirement);
            if (!string.IsNullOrWhiteSpace(tenantName))
                description = $"{description} Mandant: {tenantName}.";

            await activity.PublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        DepExportReminderMilestones.EventTypeFor(milestone),
                        Title: title,
                        Description: description,
                        Severity: DepExportReminderMilestones.SeverityFor(milestone),
                        DedupKey: dedupKey,
                        EntityType: "dep_export_requirement",
                        EntityId: requirement.Id.ToString("D"),
                        Metadata: new Dictionary<string, object>
                        {
                            ["requirementId"] = requirement.Id.ToString("D"),
                            ["requirementType"] = requirement.RequirementType,
                            ["category"] = requirement.Category,
                            ["title"] = requirement.Title,
                            ["dueDate"] = requirement.DueDate!.Value.ToString("O"),
                            ["daysUntilDue"] = daysUntilDue,
                            ["milestone"] = milestone,
                            ["periodStart"] = requirement.PeriodStart?.ToString("O") ?? "",
                            ["periodEnd"] = requirement.PeriodEnd?.ToString("O") ?? "",
                            ["deepLink"] = "/rksv/dep-export-compliance",
                        }),
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await push
                    .SendReminderAsync(tenantId, requirement, milestone, daysUntilDue, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "DEP export mobile push failed after activity publish for tenant {TenantId} requirement {RequirementId}",
                    tenantId,
                    requirement.Id);
            }

            sent++;
        }

        return (sent, skipped);
    }
}
