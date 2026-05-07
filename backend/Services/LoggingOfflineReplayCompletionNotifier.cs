using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

public sealed class LoggingOfflineReplayCompletionNotifier : IOfflineReplayCompletionNotifier
{
    private readonly ILogger<LoggingOfflineReplayCompletionNotifier> _logger;

    public LoggingOfflineReplayCompletionNotifier(ILogger<LoggingOfflineReplayCompletionNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyReplayBatchCompletedAsync(Guid cashRegisterId, int processedCount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Offline replay batch completed for POS visibility: CashRegisterId={CashRegisterId} ProcessedCount={ProcessedCount}",
            cashRegisterId,
            processedCount);
        return Task.CompletedTask;
    }
}
