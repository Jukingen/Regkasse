using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KasseAPI_Final.Data
{
    /// <summary>
    /// Design-time factory for dotnet ef. Tracked appsettings may be absent (gitignore); user secrets and env vars supply secrets.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = ResolveConfigurationBasePath();

            var configBuilder = new ConfigurationBuilder().SetBasePath(basePath);
            configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            configBuilder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
            configBuilder.AddUserSecrets(typeof(DesignTimeDbContextFactory).Assembly, optional: true);
            configBuilder.AddEnvironmentVariables();

            var config = configBuilder.Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection is not configured. Set ConnectionStrings__DefaultConnection, dotnet user-secrets, or appsettings.json (local). See backend/CONFIGURATION.md.");
            }

            optionsBuilder.UseAppNpgsql(connectionString);
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            return new AppDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Prefer backend/ when cwd is repo root or when example/local appsettings exist there.
        /// </summary>
        private static string ResolveConfigurationBasePath()
        {
            var cwd = Directory.GetCurrentDirectory();
            foreach (var path in new[] { cwd, Path.Combine(cwd, "backend") })
            {
                if (File.Exists(Path.Combine(path, "appsettings.json"))
                    || File.Exists(Path.Combine(path, "appsettings.example.json")))
                    return path;
            }

            return cwd;
        }
    }
}
