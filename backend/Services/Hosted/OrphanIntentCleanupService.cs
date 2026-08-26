using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Voids captured gateway intents that never received a fiscal <c>payment_details</c> link.
/// Does not create receipts or TSE signatures.
/// </summary>
public sealed class OrphanIntentCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<PaymentGatewayOptions> _options;
    private readonly ILogger<OrphanIntentCleanupService> _logger;

    public OrphanIntentCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PaymentGatewayOptions> options,
        ILogger<OrphanIntentCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                tenantAccessor.TenantId = null;

                var ttlDays = _options.CurrentValue.OrphanIntentTtlDays;
                if (ttlDays < 1)
                    ttlDays = 7;

                var cards = scope.ServiceProvider.GetRequiredService<ICardPaymentService>();
                var voided = await cards
                    .VoidExpiredOrphanIntentsAsync(TimeSpan.FromDays(ttlDays), stoppingToken)
                    .ConfigureAwait(false);

                if (voided > 0)
                    _logger.LogInformation("Orphan gateway intent cleanup voided {Count} row(s).", voided);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Orphan intent cleanup hosted service iteration failed.");
            }
        }
    }
}
