using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// GET/PUT <c>execution-mode</c> yanıtını üretir: kullanıcı modları, seçilebilirlik, Real önkoşul tanıları.
/// </summary>
public static class BackupExecutionModeResponseBuilder
{
    public static BackupExecutionModeResponseDto Build(
        BackupConfigurationHealthSnapshot currentSnap,
        BackupOptions options,
        IHostEnvironment environment,
        IBackupOperationalReadiness readiness)
    {
        var pgDumpHypothetical = readiness.GetConfigurationHealthAssumingAdminMode(
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump);
        var runnable = currentSnap.Level != BackupConfigurationHealthLevel.Unhealthy;
        var stored = currentSnap.AdminRuntimeExecutionMode;
        var blocking = BackupExecutionModeApiMapper.FilterRealModeBlockingDiagnostics(pgDumpHypothetical);
        var selectable = BackupExecutionModeApiMapper.BuildSelectableModes(options, environment, pgDumpHypothetical);

        return new BackupExecutionModeResponseDto
        {
            StoredMode = stored.ToString(),
            RequestedUserFacingMode = BackupExecutionModeApiMapper.ToUserFacingMode(stored),
            ConfigurationDefaultUserFacingMode =
                BackupExecutionModeApiMapper.AdapterKindToUserFacingMode(options.ExecutionAdapterKind),
            EffectiveUserFacingMode = BackupExecutionModeApiMapper.AdapterKindToUserFacingMode(currentSnap.EffectiveAdapterKind),
            RecommendedFallbackUserFacingMode =
                BackupExecutionModeApiMapper.RecommendedFallbackUserFacingMode(stored, runnable),
            AdapterKindIfConfigurationDefaultOnly =
                BackupExecutionModeApiMapper.AdapterIfUsingConfigurationDefaultOnly(options).ToString(),
            EffectiveModeResolutionSummaryEnglish =
                BackupExecutionModeApiMapper.BuildResolutionSummaryEnglish(currentSnap, options, stored, runnable),
            ConfigurationExecutionAdapterKind = currentSnap.ConfigurationExecutionAdapterKind.ToString(),
            EffectiveExecutionAdapterKind = currentSnap.EffectiveAdapterKind.ToString(),
            EffectiveModeRunnable = runnable,
            HypotheticalPgDumpHealthLevel = pgDumpHypothetical.Level.ToString(),
            Blockers = runnable ? Array.Empty<string>() : currentSnap.Issues.ToList(),
            RealModeBlockingDiagnostics = blocking.Select(BackupConfigurationHealthResponseMapper.FromDiagnostic).ToList(),
            SelectableModes = selectable,
            EffectiveConfigurationHealth = BackupConfigurationHealthResponseMapper.FromSnapshot(currentSnap)
        };
    }
}
