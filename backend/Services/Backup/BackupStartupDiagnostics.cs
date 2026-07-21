using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public static class BackupStartupDiagnostics
{
    public static void LogAtStartup(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<BackupOptions>>().Value;
        var restoreOpts = scope.ServiceProvider.GetRequiredService<IOptions<RestoreVerificationOptions>>().Value;
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var probeState = scope.ServiceProvider.GetRequiredService<IBackupPostgresClientToolingProbeState>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BackupStartup");

        var snap = BackupConfigurationEvaluation.Evaluate(opts, env, configuration);
        if (snap.Level == BackupConfigurationHealthLevel.Healthy)
        {
            logger.LogInformation(
                "Backup orchestration: health={Level}, adapterKind={Adapter}, executionReality={Reality}, realPostgreSqlLogicalDump={RealPg}, workerEnabled={Worker}",
                snap.Level,
                snap.EffectiveAdapterKind,
                snap.BackupExecutionReality,
                snap.RealPostgreSqlLogicalDumpConfigured,
                snap.WorkerEnabled);
        }
        else
        {
            logger.LogWarning(
                "Backup orchestration: health={Level}, adapterKind={Adapter}, executionReality={Reality}, realPostgreSqlLogicalDump={RealPg}, workerEnabled={Worker}, narrative={Narrative}, issues={@Issues}",
                snap.Level,
                snap.EffectiveAdapterKind,
                snap.BackupExecutionReality,
                snap.RealPostgreSqlLogicalDumpConfigured,
                snap.WorkerEnabled,
                snap.ReadinessNarrative,
                snap.Issues);
        }

        var diagnosticCodes = snap.Diagnostics.Select(d => d.Code).ToArray();
        if (diagnosticCodes.Length > 0)
        {
            logger.LogInformation(
                "Backup configuration diagnostics (machine codes): {DiagnosticCodes}",
                diagnosticCodes);
        }

        BackupDevelopmentToolingDiagnostics.LogPostgresClientToolingIfDevelopment(env, logger, opts, restoreOpts, probeState);

        var toolingSnap = probeState.Snapshot;
        if (!toolingSnap.ProbesSkipped && env.IsDevelopment() && opts.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump)
        {
            logger.LogInformation(
                "Backup development tooling summary: pg_dump_ok={PgDumpOk}, pg_restore_ok={PgRestoreOk}, pg_dump_failureKind={PgDumpFail}, pg_restore_failureKind={PgRestoreFail}",
                toolingSnap.PgDumpProbeSucceeded,
                toolingSnap.PgRestoreProbeSucceeded,
                toolingSnap.PgDumpFailureKind,
                toolingSnap.PgRestoreFailureKind);
        }
    }
}
