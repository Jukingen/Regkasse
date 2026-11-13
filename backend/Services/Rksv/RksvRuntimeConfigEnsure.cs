using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Rksv;

internal static class RksvRuntimeConfigEnsure
{
    public static async Task EnsureSingletonAsync(
        AppDbContext db,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        CancellationToken cancellationToken)
    {
        if (await db.RksvRuntimeConfigs.AsNoTracking()
                .AnyAsync(x => x.Id == RksvRuntimeConfig.SingletonId, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        db.RksvRuntimeConfigs.Add(SeedFromConfiguration(configuration, hostEnvironment));
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.RksvRuntimeConfigs.AsNoTracking()
                    .AnyAsync(x => x.Id == RksvRuntimeConfig.SingletonId, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw;
            }
        }
    }

    public static RksvRuntimeConfig SeedFromConfiguration(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var modeRaw = configuration["RKSV:Mode"];
        if (string.IsNullOrWhiteSpace(modeRaw)
            && (hostEnvironment.IsDevelopment() || hostEnvironment.IsStaging()))
        {
            modeRaw = RksvRuntimeConfig.ModeDemo;
        }

        var mode = RksvRuntimeConfig.NormalizeMode(modeRaw);
        var tseMode = RksvRuntimeConfig.NormalizeIntegrationMode(configuration["RKSV:TseMode"]);
        var fonMode = RksvRuntimeConfig.NormalizeIntegrationMode(configuration["RKSV:FinanzOnlineMode"]);
        var showDemoLabel = configuration.GetValue("RKSV:ShowDemoLabel", mode == RksvRuntimeConfig.ModeDemo);
        var bypassTseInDevelopment = configuration.GetValue("DevelopmentOptions:BypassTseInDevelopment", false);

        return RksvRuntimeConfig.CreateFromAppsettings(
            mode, tseMode, fonMode, showDemoLabel, bypassTseInDevelopment);
    }
}
