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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LicenseComplianceHostedService> _logger;

    /// <summary>Creates the hosted compliance loop (startup re-check and 24-hour timer).</summary>
    /// <param name="scopeFactory">Used to resolve scoped <see cref="ILicenseService"/> in production hosting.</param>
    /// <param name="lifetime">Host application lifetime (wait for startup before first check).</param>
    /// <param name="logger">Structured logger.</param>
    public LicenseComplianceHostedService(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<LicenseComplianceHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        await WaitForApplicationStartedAsync(_lifetime, stoppingToken).ConfigureAwait(false);

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
            using var scope = _scopeFactory.CreateScope();
            var lic = scope.ServiceProvider.GetRequiredService<ILicenseService>();
            lic.EvaluateOnStartup();
            lic.GetCurrentStatusAsync(CancellationToken.None).GetAwaiter().GetResult();
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
