using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupOperationalReadinessService : IBackupOperationalReadiness
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IBackupArtifactExternalArchive _externalArchive;
    private readonly IBackupPostgresClientToolingProbeState _postgresToolingProbeState;
    private readonly IServiceScopeFactory _scopeFactory;

    public BackupOperationalReadinessService(
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment environment,
        IConfiguration configuration,
        IBackupArtifactExternalArchive externalArchive,
        IBackupPostgresClientToolingProbeState postgresToolingProbeState,
        IServiceScopeFactory scopeFactory)
    {
        _options = options;
        _environment = environment;
        _configuration = configuration;
        _externalArchive = externalArchive;
        _postgresToolingProbeState = postgresToolingProbeState;
        _scopeFactory = scopeFactory;
    }

    private (AdminBackupRuntimeExecutionMode adminMode, BackupExecutionAdapterKind effectiveKind) LoadExecutionProfile()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.BackupRuntimeExecutionPreferences.AsNoTracking()
            .FirstOrDefault(x => x.Id == BackupRuntimeExecutionPreference.SingletonId);
        var mode = row?.Mode ?? AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
        var effective = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(_options.CurrentValue, mode);
        return (mode, effective);
    }

    private BackupConfigurationHealthSnapshot MergeToolingDiagnostics(BackupConfigurationHealthSnapshot snap)
    {
        var tooling = _postgresToolingProbeState.Snapshot;
        if (tooling.ProbesSkipped)
            return snap;

        var toolingDiagnostics = tooling.ToDiagnostics();
        var merged = snap.Diagnostics.Concat(toolingDiagnostics).ToList();
        var level = BackupConfigurationHealthLevelAggregator.CombineWithDiagnostics(snap.Level, toolingDiagnostics);
        return new BackupConfigurationHealthSnapshot
        {
            Level = level,
            Issues = snap.Issues,
            Diagnostics = merged,
            EffectiveAdapterKind = snap.EffectiveAdapterKind,
            ConfigurationExecutionAdapterKind = snap.ConfigurationExecutionAdapterKind,
            AdminRuntimeExecutionMode = snap.AdminRuntimeExecutionMode,
            WorkerEnabled = snap.WorkerEnabled,
            RealPostgreSqlLogicalDumpConfigured = snap.RealPostgreSqlLogicalDumpConfigured,
            BackupExecutionReality = snap.BackupExecutionReality,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = snap.NonRealBackupAdapterAcknowledgmentConfigurationKey,
            ReadinessNarrative = snap.ReadinessNarrative,
            ArtifactVerificationDisclaimer = snap.ArtifactVerificationDisclaimer,
            RetentionReadiness = snap.RetentionReadiness,
            ExternalArchiveReadiness = snap.ExternalArchiveReadiness
        };
    }

    public BackupConfigurationHealthSnapshot GetConfigurationHealthAssumingAdminMode(AdminBackupRuntimeExecutionMode adminMode)
    {
        var effectiveKind = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(_options.CurrentValue, adminMode);
        var snap = BackupConfigurationEvaluation.Evaluate(
            _options.CurrentValue,
            _environment,
            _configuration,
            _externalArchive.BackendDescriptor,
            effectiveKind,
            adminMode);
        return MergeToolingDiagnostics(snap);
    }

    public BackupConfigurationHealthSnapshot GetConfigurationHealth()
    {
        var (adminMode, _) = LoadExecutionProfile();
        return GetConfigurationHealthAssumingAdminMode(adminMode);
    }

    public BackupArtifactPipelinePolicySnapshot GetArtifactPipelinePolicy()
    {
        var (_, effectiveKind) = LoadExecutionProfile();
        return BackupArtifactPipelinePolicyEvaluator.Evaluate(
            _options.CurrentValue,
            _environment,
            _externalArchive.BackendDescriptor,
            effectiveKind);
    }
}
