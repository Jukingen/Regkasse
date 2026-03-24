using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace KasseAPI_Final.Data
{
    /// <summary>
    /// Design-time factory for dotnet ef. Must not require appsettings.json (CI checkouts omit gitignored files).
    /// Connection string: environment (ConnectionStrings__DefaultConnection) overrides optional JSON files.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = ResolveConfigurationBasePath();

            // Only register JSON providers when files exist (CI has no gitignored appsettings; avoids any AddJsonFile edge cases).
            var configBuilder = new ConfigurationBuilder().SetBasePath(basePath);
            var appSettingsPath = Path.Combine(basePath, "appsettings.json");
            if (File.Exists(appSettingsPath))
                configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            var devSettingsPath = Path.Combine(basePath, "appsettings.Development.json");
            if (File.Exists(devSettingsPath))
                configBuilder.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: false);
            configBuilder.AddEnvironmentVariables();

            var config = configBuilder.Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection is not configured. Set environment variable ConnectionStrings__DefaultConnection " +
                    "or add ConnectionStrings:DefaultConnection to appsettings.json (optional in CI).");
            }

            optionsBuilder.UseAppNpgsql(connectionString);
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            return new AppDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Prefer a directory that contains appsettings.json (e.g. backend/ when the tool is invoked from repo root).
        /// Falls back to the current directory so optional JSON can be skipped and env-based config still works.
        /// </summary>
        private static string ResolveConfigurationBasePath()
        {
            var cwd = Directory.GetCurrentDirectory();
            foreach (var path in new[] { cwd, Path.Combine(cwd, "backend") })
            {
                if (File.Exists(Path.Combine(path, "appsettings.json")))
                    return path;
            }

            return cwd;
        }
    }
}
