using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Push;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportPushNotificationService
{
    Task SendReminderAsync(
        Guid tenantId,
        DepExportRequirement requirement,
        string milestone,
        int daysUntilDue,
        CancellationToken cancellationToken = default);

    Task SendOverdueAlertAsync(
        Guid tenantId,
        DepExportRequirement requirement,
        CancellationToken cancellationToken = default);

    Task SendSuccessNotificationAsync(
        Guid tenantId,
        string exportName,
        CancellationToken cancellationToken = default);

    Task<DepExportMobilePushSettings> GetSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportMobilePushSettings> SaveSettingsAsync(
        Guid tenantId,
        DepExportMobilePushSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends DEP export mobile push to tenant Mandanten-Admins (Manager role).
/// Delivery goes through <see cref="IPushNotificationService"/> (logging stub until FCM/Expo is wired).
/// </summary>
public sealed class DepExportPushNotificationService : IDepExportPushNotificationService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly INotificationConfigService _notificationConfig;
    private readonly TimeProvider _time;
    private readonly ILogger<DepExportPushNotificationService> _logger;

    public DepExportPushNotificationService(
        AppDbContext db,
        IPushNotificationService pushNotificationService,
        INotificationConfigService notificationConfig,
        TimeProvider time,
        ILogger<DepExportPushNotificationService> logger)
    {
        _db = db;
        _pushNotificationService = pushNotificationService;
        _notificationConfig = notificationConfig;
        _time = time;
        _logger = logger;
    }

    public async Task<DepExportMobilePushSettings> GetSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var config = await _notificationConfig.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return config.DepExportMobilePush ?? DepExportMobilePushSettings.CreateDefault();
    }

    public async Task<DepExportMobilePushSettings> SaveSettingsAsync(
        Guid tenantId,
        DepExportMobilePushSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var config = await _notificationConfig.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        config.DepExportMobilePush = settings;
        var saved = await _notificationConfig.SaveAsync(tenantId, config, cancellationToken).ConfigureAwait(false);
        return saved.DepExportMobilePush ?? settings;
    }

    public async Task SendReminderAsync(
        Guid tenantId,
        DepExportRequirement requirement,
        string milestone,
        int daysUntilDue,
        CancellationToken cancellationToken = default)
    {
        if (milestone == DepExportReminderMilestones.Overdue)
        {
            await SendOverdueAlertAsync(tenantId, requirement, cancellationToken).ConfigureAwait(false);
            return;
        }

        var settings = await GetSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!settings.IsMilestoneEnabled(milestone))
        {
            _logger.LogDebug(
                "DEP push reminder skipped (settings): tenant={TenantId} milestone={Milestone}",
                tenantId,
                milestone);
            return;
        }

        var title = DepExportReminderMilestones.BuildTitle(milestone);
        var body = BuildReminderBody(requirement, daysUntilDue);
        await SendToTenantAdminsAsync(
                tenantId,
                title,
                body,
                BuildData("DepExportReminder", requirement, milestone, daysUntilDue),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendOverdueAlertAsync(
        Guid tenantId,
        DepExportRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!settings.IsMilestoneEnabled(DepExportReminderMilestones.Overdue))
        {
            _logger.LogDebug("DEP overdue push skipped (settings): tenant={TenantId}", tenantId);
            return;
        }

        var daysUntilDue = requirement.DueDate is DateTime due
            ? DepExportReminderMilestones.DaysUntilDue(due, _time.GetUtcNow().UtcDateTime)
            : -1;

        await SendToTenantAdminsAsync(
                tenantId,
                DepExportReminderMilestones.BuildTitle(DepExportReminderMilestones.Overdue),
                DepExportReminderMilestones.BuildGermanMessage(
                    DepExportReminderMilestones.Overdue,
                    requirement),
                BuildData("DepExportOverdue", requirement, DepExportReminderMilestones.Overdue, daysUntilDue),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendSuccessNotificationAsync(
        Guid tenantId,
        string exportName,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!settings.PushEnabled || !settings.SuccessNotification)
            return;

        var name = string.IsNullOrWhiteSpace(exportName) ? "DEP Export" : exportName.Trim();
        await SendToTenantAdminsAsync(
                tenantId,
                "DEP Export erfolgreich",
                $"{name} wurde erfolgreich erstellt.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Type"] = "DepExportSuccess",
                    ["ExportName"] = name,
                    ["DeepLink"] = "/rksv/dep-export-compliance",
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendToTenantAdminsAsync(
        Guid tenantId,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var admins = await GetTenantAdminsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (admins.Count == 0)
        {
            _logger.LogInformation(
                "DEP push: no Manager recipients for tenant {TenantId}",
                tenantId);
            return;
        }

        var sent = 0;
        foreach (var admin in admins)
        {
            var ok = await _pushNotificationService
                .SendAsync(
                    new PushNotification
                    {
                        UserId = admin.Id,
                        Title = title,
                        Body = body,
                        Data = data,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (ok)
                sent++;
        }

        _logger.LogInformation(
            "DEP push dispatched: tenant={TenantId} recipients={Recipients} sent={Sent} title={Title}",
            tenantId,
            admins.Count,
            sent,
            title);
    }

    private async Task<IReadOnlyList<ApplicationUser>> GetTenantAdminsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var managers = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { Membership = m, User = u })
            .Where(x => x.User.IsActive && x.User.Role == Roles.Manager)
            .Select(x => x.User)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (managers.Count > 0)
            return managers;

        // Fallback: active owner when no Manager-role users exist.
        var owner = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive && m.IsOwner)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (_, u) => u)
            .Where(u => u.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return owner is null ? Array.Empty<ApplicationUser>() : new[] { owner };
    }

    private static string BuildReminderBody(DepExportRequirement requirement, int daysUntilDue)
    {
        var days = Math.Max(daysUntilDue, 0);
        return $"{requirement.Title} ist in {days} Tagen fällig.";
    }

    private static Dictionary<string, string> BuildData(
        string type,
        DepExportRequirement requirement,
        string milestone,
        int daysUntilDue) =>
        new(StringComparer.Ordinal)
        {
            ["Type"] = type,
            ["RequirementId"] = requirement.Id.ToString("D"),
            ["Milestone"] = milestone,
            ["DueDate"] = requirement.DueDate?.ToString("O") ?? string.Empty,
            ["DaysUntilDue"] = daysUntilDue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DeepLink"] = "/rksv/dep-export-compliance",
        };
}
