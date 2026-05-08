using KasseAPI_Final;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Re-evaluates license after the host has started (non-blocking) and on a 24-hour schedule.
/// </summary>
public sealed class LicenseComplianceHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<LicenseComplianceHostedService> _logger;

    public LicenseComplianceHostedService(
        IServiceProvider services,
        ILogger<LicenseComplianceHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        var lifetime = _services.GetRequiredService<IHostApplicationLifetime>();
        await WaitForApplicationStartedAsync(lifetime, stoppingToken).ConfigureAwait(false);

        _ = Task.Run(RunLicenseComplianceCheck, CancellationToken.None);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            RunLicenseComplianceCheck();
        }
    }

    private static Task WaitForApplicationStartedAsync(IHostApplicationLifetime lifetime, CancellationToken stoppingToken)
    {
        if (lifetime.ApplicationStarted.IsCancellationRequested)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
        return tcs.Task.WaitAsync(stoppingToken);
    }

    private void RunLicenseComplianceCheck()
    {
        try
        {
            var lic = _services.GetRequiredService<ILicenseService>();
            lic.EvaluateOnStartup();
            var s = lic.GetStatus();
            if (!s.IsValid && !s.IsTrial)
                _logger.LogWarning("Lizenz ungültig oder abgelaufen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License compliance check failed.");
        }
    }
}
