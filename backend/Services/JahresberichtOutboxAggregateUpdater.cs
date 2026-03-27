using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// FinanzOnline outbox sonrası Jahresbericht rapor satırını günceller.
/// </summary>
public static class JahresberichtOutboxAggregateUpdater
{
    public static async Task ApplyAfterOutboxPersistAsync(
        AppDbContext context,
        FinanzOnlineOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(message.AggregateType, "JahresberichtReport", StringComparison.OrdinalIgnoreCase))
            return;

        var report = await context.Set<JahresberichtReport>()
            .FirstOrDefaultAsync(x => x.Id == message.AggregateId, cancellationToken)
            .ConfigureAwait(false);
        if (report == null)
            return;

        report.LastFinanzOnlineOutboxMessageId = message.Id;
        report.LastSubmissionStatusCode = message.Status;
        report.LastSubmissionError = message.LastErrorMessage;

        if (message.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess)
            report.LastSubmissionError = null;
    }
}
