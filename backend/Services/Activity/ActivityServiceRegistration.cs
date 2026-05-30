using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services.Activity;

/// <summary>
/// Activity feed, notifications, and backup-alert-to-activity mapping.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ActivityEventRecorder"/> is a <strong>singleton</strong> that resolves scoped
/// <see cref="IActivityEventService"/> via <see cref="IServiceScopeFactory"/> (same pattern as
/// <see cref="LicenseService"/> and <see cref="ActivityBackupAlertPublisher"/>).
/// </para>
/// <para>
/// Domain-specific types such as <c>ActivityUserEventPublisher</c> are not used in this codebase;
/// user/cash-register/license events publish through <see cref="IActivityEventPublisher"/>.
/// </para>
/// <para>
/// <see cref="ActivityBackupAlertPublisher"/> implements <see cref="Backup.IBackupAlertPublisher"/>, not
/// <see cref="IActivityEventPublisher"/>; it is registered here but composed into
/// <see cref="Backup.CompositeBackupAlertPublisher"/> from application host backup registration.
/// </para>
/// </remarks>
public static class ActivityServiceRegistration
{
    public static IServiceCollection AddActivityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ActivityNotificationOptions>(
            configuration.GetSection(ActivityNotificationOptions.SectionName));

        services.AddSingleton<IActivityStreamHub, ActivityStreamHub>();
        services.AddScoped<INotificationConfigService, NotificationConfigService>();
        services.AddScoped<IActivityEventService, ActivityEventService>();
        services.AddScoped<IActivityEventEmailNotifier, ActivityEventEmailNotifier>();
        services.AddScoped<IActivityEventWebhookNotifier, ActivityEventWebhookNotifier>();
        services
            .AddHttpClient(ActivityEventWebhookNotifier.HttpClientName)
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<ActivityEventRecorder>();
        services.AddSingleton<IActivityEventPublisher>(sp => sp.GetRequiredService<ActivityEventRecorder>());
        services.AddSingleton<ActivityBackupAlertPublisher>();

        services.AddHostedService<ActivityMonitoringHostedService>();
        services.AddHostedService<ActivityEventCleanupHostedService>();

        services.Configure<SuspiciousTransactionDetectionOptions>(
            configuration.GetSection(SuspiciousTransactionDetectionOptions.SectionName));
        services.AddScoped<ISuspiciousTransactionAlertService, SuspiciousTransactionAlertService>();
        services.AddScoped<SuspiciousTransactionDetector>();
        services.AddHostedService<SuspiciousTransactionDetectionHostedService>();

        return services;
    }
}
