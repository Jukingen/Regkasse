using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Polls for missing-Monatsbeleg reminders and Auto-Monatsbeleg (Vienna 1st 00:01, catch-up days 1–7).
/// </summary>
public sealed class MonatsbelegSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MonatsbelegOpsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MonatsbelegSchedulerHostedService> _logger;

    public MonatsbelegSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MonatsbelegOpsOptions> options,
        TimeProvider time,
        ILogger<MonatsbelegSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opt = _options.CurrentValue;
                var intervalMinutes = ResolveIntervalMinutes(opt);
                var delay = AutoMonatsbelegCutoff.GetDelayUntilNextSweep(
                    _time.GetUtcNow().UtcDateTime,
                    intervalMinutes,
                    allowImmediateIfInWindow: first);
                first = false;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var ops = scope.ServiceProvider.GetRequiredService<IMonatsbelegOpsService>();

                var reminded = await ops.PublishMissingRemindersAsync(stoppingToken).ConfigureAwait(false);
                var created = await ops.RunAutoCreateAsync(stoppingToken).ConfigureAwait(false);
                if (reminded > 0 || created > 0)
                {
                    _logger.LogInformation(
                        "Monatsbeleg scheduler sweep: reminders={Reminded} autoCreated={Created}",
                        reminded,
                        created);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Monatsbeleg scheduler hosted service iteration failed.");
            }
        }
    }

    internal static int ResolveIntervalMinutes(MonatsbelegOpsOptions opt)
    {
        if (opt.CheckIntervalMinutes > 0)
            return Math.Max(5, opt.CheckIntervalMinutes);
        if (opt.CheckIntervalHours > 0)
            return Math.Max(5, opt.CheckIntervalHours * 60);
        return 15;
    }
}
