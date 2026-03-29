using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

public sealed class LoggingBackupAlertPublisher : IBackupAlertPublisher
{
    private readonly ILogger<LoggingBackupAlertPublisher> _logger;

    public LoggingBackupAlertPublisher(ILogger<LoggingBackupAlertPublisher> logger)
    {
        _logger = logger;
    }

    public void Publish(BackupAlertEvent evt)
    {
        if (evt.Data is { Count: > 0 } data)
        {
            _logger.LogWarning(
                "Backup alert: kind={Kind}, runId={RunId}, correlationId={CorrelationId}, message={Message}, data={@Data}",
                evt.Kind,
                evt.BackupRunId,
                evt.CorrelationId,
                evt.Message,
                data);
            return;
        }

        _logger.LogWarning(
            "Backup alert: kind={Kind}, runId={RunId}, correlationId={CorrelationId}, message={Message}",
            evt.Kind,
            evt.BackupRunId,
            evt.CorrelationId,
            evt.Message);
    }
}
