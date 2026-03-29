using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore verification config sağlığı; backup config değerlendirmesi ile birleştirilmez.
/// </summary>
public static class RestoreVerificationConfigurationEvaluation
{
    public static RestoreVerificationConfigurationHealthSnapshot Evaluate(
        RestoreVerificationOptions options,
        IHostEnvironment environment)
    {
        var issues = new List<string>();
        var level = RestoreVerificationConfigurationHealthLevel.Healthy;

        void Add(RestoreVerificationConfigurationHealthLevel minLevel, string message)
        {
            issues.Add(message);
            if ((int)minLevel > (int)level)
                level = minLevel;
        }

        if (!options.WorkerEnabled)
            Add(RestoreVerificationConfigurationHealthLevel.Degraded,
                "RestoreVerification:WorkerEnabled=false — queued drills will not be processed.");

        if (!options.OrchestratorDistributedLockEnabled && !environment.IsDevelopment())
        {
            Add(RestoreVerificationConfigurationHealthLevel.Degraded,
                "RestoreVerification:OrchestratorDistributedLockEnabled=false — multiple API replicas can race on the same Queued restore row or weekly enqueue; use a single worker replica or enable the PostgreSQL advisory lock.");
        }

        if (options.OrchestratorPollingInterval < TimeSpan.FromSeconds(1))
            Add(RestoreVerificationConfigurationHealthLevel.Unhealthy,
                "RestoreVerification:OrchestratorPollingInterval must be at least 00:00:01.");

        if (options.OrchestratorPollingInterval > TimeSpan.FromHours(24))
            Add(RestoreVerificationConfigurationHealthLevel.Unhealthy,
                "RestoreVerification:OrchestratorPollingInterval exceeds 24 hours (misconfiguration).");

        return new RestoreVerificationConfigurationHealthSnapshot
        {
            Level = level,
            Issues = issues,
            WorkerEnabled = options.WorkerEnabled,
            OrchestratorDistributedLockEnabled = options.OrchestratorDistributedLockEnabled
        };
    }
}
