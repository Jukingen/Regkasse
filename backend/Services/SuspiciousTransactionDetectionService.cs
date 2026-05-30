using KasseAPI_Final;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Background scan for suspicious payment patterns across active tenants.
/// Persists alerts and publishes to the admin activity feed (email/webhook when configured).
/// </summary>
public sealed class SuspiciousTransactionDetectionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SuspiciousTransactionDetectionOptions> _options;
    private readonly ILogger<SuspiciousTransactionDetectionHostedService> _logger;

    public SuspiciousTransactionDetectionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SuspiciousTransactionDetectionOptions> options,
        ILogger<SuspiciousTransactionDetectionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled || !_options.CurrentValue.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Clamp(_options.CurrentValue.ScanIntervalMinutes, 1, 120);
            try
            {
                await DetectSuspiciousTransactionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Suspicious transaction detection cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
        }
    }

    private async Task DetectSuspiciousTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            using var tenantScope = _scopeFactory.CreateScope();
            var detector = tenantScope.ServiceProvider.GetRequiredService<SuspiciousTransactionDetector>();
            try
            {
                await detector.DetectForTenantAsync(tenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Suspicious transaction detection failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
