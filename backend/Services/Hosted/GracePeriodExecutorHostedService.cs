using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.GracePeriods;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Executes deferred grace-period actions after the undo window ends.</summary>
public sealed class GracePeriodExecutorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<GracePeriodsOptions> _options;
    private readonly ILogger<GracePeriodExecutorHostedService> _logger;

    public GracePeriodExecutorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<GracePeriodsOptions> options,
        ILogger<GracePeriodExecutorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opts = _options.CurrentValue;
                if (opts.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IGracePeriodService>();
                    var n = await svc.ExecuteDueAsync(stoppingToken).ConfigureAwait(false);
                    if (n > 0)
                        _logger.LogInformation("Grace period executor completed {Count} due action(s)", n);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Grace period executor loop failed");
            }

            var delay = Math.Clamp(_options.CurrentValue.ExecutorPollSeconds, 10, 300);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
