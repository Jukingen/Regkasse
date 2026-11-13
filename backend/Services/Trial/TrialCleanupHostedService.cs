using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Trial;

/// <summary>
/// Daily soft-archive of expired trials after grace + <see cref="TrialOptions.AutoDeleteAfterGraceDays"/>.
/// Does not hard-wipe RKSV/fiscal data.
/// </summary>
public sealed class TrialCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TrialOptions> _options;
    private readonly ILogger<TrialCleanupHostedService> _logger;

    public TrialCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TrialOptions> options,
        ILogger<TrialCleanupHostedService> logger)
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
            var delay = ComputeDelayUntilUtc(
                _options.CurrentValue.CleanupHourUtc,
                _options.CurrentValue.CleanupMinuteUtc);
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Trial cleanup cycle failed.");
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
            return;

        using var scope = _scopeFactory.CreateScope();
        var trial = scope.ServiceProvider.GetRequiredService<ITrialService>();
        // Ensure grace markers exist before cleanup.
        await trial.ProcessExpiryAndGraceAsync(cancellationToken).ConfigureAwait(false);
        var cleaned = await trial.ProcessCleanupAsync(cancellationToken).ConfigureAwait(false);
        if (cleaned > 0)
            _logger.LogInformation("Trial cleanup soft-archived {Count} tenant(s).", cleaned);
    }

    private static TimeSpan ComputeDelayUntilUtc(int hourUtc, int minuteUtc)
    {
        var hour = Math.Clamp(hourUtc, 0, 23);
        var minute = Math.Clamp(minuteUtc, 0, 59);
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }
}
