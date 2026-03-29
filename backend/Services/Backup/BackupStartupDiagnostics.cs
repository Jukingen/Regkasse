using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public static class BackupStartupDiagnostics
{
    public static void LogAtStartup(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<BackupOptions>>().Value;
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BackupStartup");

        var snap = BackupConfigurationEvaluation.Evaluate(opts, env, configuration);
        if (snap.Level == BackupConfigurationHealthLevel.Healthy)
        {
            logger.LogInformation(
                "Backup orchestration: health={Level}, adapterKind={Adapter}, workerEnabled={Worker}",
                snap.Level,
                snap.EffectiveAdapterKind,
                snap.WorkerEnabled);
            return;
        }

        logger.LogWarning(
            "Backup orchestration: health={Level}, adapterKind={Adapter}, workerEnabled={Worker}, issues={@Issues}",
            snap.Level,
            snap.EffectiveAdapterKind,
            snap.WorkerEnabled,
            snap.Issues);
    }
}
