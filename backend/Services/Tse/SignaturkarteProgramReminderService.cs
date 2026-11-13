using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public interface ISignaturkarteProgramReminderService
{
    /// <summary>
    /// Daily sweep: when a configured milestone matches days-until-deadline,
    /// publish Activity (email/webhook via notification config) for tenants with Open devices.
    /// </summary>
    Task<SignaturkarteProgramReminderSweepResult> CheckAndNotifyAsync(
        CancellationToken cancellationToken = default);
}

public sealed record SignaturkarteProgramReminderSweepResult(
    int TenantsScanned,
    int RemindersSent,
    int Skipped);

public sealed class SignaturkarteProgramReminderService : ISignaturkarteProgramReminderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SignaturkarteProgramOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SignaturkarteProgramReminderService> _logger;

    public SignaturkarteProgramReminderService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SignaturkarteProgramOptions> options,
        TimeProvider timeProvider,
        ILogger<SignaturkarteProgramReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SignaturkarteProgramReminderSweepResult> CheckAndNotifyAsync(
        CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return new SignaturkarteProgramReminderSweepResult(0, 0, 0);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var daysUntil = SignaturkarteProgramMilestones.DaysUntilDeadline(opt.DeadlineUtc, now);
        var milestone = SignaturkarteProgramMilestones.ResolveMilestone(
            daysUntil,
            opt.ReminderDaysBefore,
            opt.SendOverdueReminders);

        if (milestone is null)
            return new SignaturkarteProgramReminderSweepResult(0, 0, 0);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var tenantAccessor = sp.GetRequiredService<ICurrentTenantAccessor>();
        tenantAccessor.TenantId = null;

        var db = sp.GetRequiredService<AppDbContext>();
        var activity = sp.GetRequiredService<IActivityEventService>();
        var audit = sp.GetRequiredService<IAuditLogService>();

        var devices = await db.TseDevices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(d => d.Tenant)
            .Where(d => d.TenantId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var openByTenant = devices
            .GroupBy(d => d.TenantId!.Value)
            .Select(g =>
            {
                var open = g.Count(d =>
                    SignaturkarteProgramClassifier.Classify(d, opt.ExcludeDemoAndSoftDevices)
                    == SignaturkarteProgramStatuses.Open);
                var tenant = g.First().Tenant;
                return new
                {
                    TenantId = g.Key,
                    TenantName = tenant?.Name,
                    OpenCount = open,
                };
            })
            .Where(x => x.OpenCount > 0)
            .ToList();

        var sent = 0;
        var skipped = 0;

        foreach (var row in openByTenant)
        {
            try
            {
                tenantAccessor.TenantId = row.TenantId;
                var scopeKey = row.TenantId.ToString("D");
                var dedupKey = SignaturkarteProgramMilestones.BuildDedupKey(
                    opt.DeadlineUtc,
                    milestone,
                    scopeKey,
                    now);

                var alreadySent = await db.ActivityEvents
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(
                        e => e.TenantId == row.TenantId && e.DedupKey == dedupKey,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (alreadySent)
                {
                    skipped++;
                    continue;
                }

                var title = SignaturkarteProgramMilestones.BuildTitle(milestone, opt.DisplayName);
                var description = SignaturkarteProgramMilestones.BuildGermanMessage(
                    milestone,
                    daysUntil,
                    row.OpenCount,
                    opt.DeadlineUtc);
                if (!string.IsNullOrWhiteSpace(row.TenantName))
                    description = $"{description} Mandant: {row.TenantName}.";

                await activity.PublishAsync(
                        new ActivityEventPublishRequest(
                            row.TenantId,
                            SignaturkarteProgramMilestones.EventTypeFor(milestone),
                            Title: title,
                            Description: description,
                            Severity: SignaturkarteProgramMilestones.SeverityFor(milestone, daysUntil),
                            DedupKey: dedupKey,
                            EntityType: "signaturkarte_program",
                            EntityId: opt.DeadlineUtc.ToUniversalTime().ToString("yyyyMMdd"),
                            Metadata: new Dictionary<string, object>
                            {
                                ["deadlineUtc"] = opt.DeadlineUtc.ToUniversalTime().ToString("O"),
                                ["daysRemaining"] = daysUntil,
                                ["milestone"] = milestone,
                                ["nonCompliantDeviceCount"] = row.OpenCount,
                                ["tenantId"] = row.TenantId.ToString("D"),
                                ["displayName"] = opt.DisplayName,
                                ["isCertificateExpiry"] = false,
                                ["deepLink"] = "/admin/tse/signaturkarte-program",
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    await audit.LogSystemOperationAsync(
                            action: "SIGNATURKARTE_PROGRAM_REMINDER_SENT",
                            entityType: "SignaturkarteProgram",
                            userId: "system",
                            userRole: "System",
                            description: title,
                            notes: description,
                            actionType: AuditEventType.SignaturkarteProgramReminderSent,
                            tenantId: row.TenantId,
                            newValues: new
                            {
                                milestone,
                                daysRemaining = daysUntil,
                                openCount = row.OpenCount,
                                deadlineUtc = opt.DeadlineUtc.ToUniversalTime(),
                            })
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "Audit failed after Signaturkarte reminder for tenant {TenantId}",
                        row.TenantId);
                }

                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Signaturkarte program reminder failed for tenant {TenantId}",
                    row.TenantId);
            }
            finally
            {
                tenantAccessor.TenantId = null;
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "Signaturkarte program reminders published: tenants={Tenants} sent={Sent} skipped={Skipped} milestone={Milestone}",
                openByTenant.Count,
                sent,
                skipped,
                milestone);
        }

        return new SignaturkarteProgramReminderSweepResult(openByTenant.Count, sent, skipped);
    }
}
