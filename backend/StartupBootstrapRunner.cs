using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final
{
    /// <summary>
    /// Orchestrates all startup bootstrap responsibilities: database migration,
    /// pending migration gate, role/user seeding, demo data, product seed, guest customer seed.
    /// No behavior change — extraction only for clarity and future W1-T02/T03 work.
    /// </summary>
    public static class StartupBootstrapRunner
    {
        /// <summary>
        /// Runs the full bootstrap sequence using services from the given scope.
        /// Execution order and semantics match the previous inline implementation in Program.cs.
        /// </summary>
        /// <param name="serviceProvider">Scoped service provider (e.g. from app.Services.CreateScope()).</param>
        public static async Task RunAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            var context = serviceProvider.GetRequiredService<AppDbContext>();

            // Startup Migration Check Gate: halt if schema is out of date
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KasseAPI_Final.StartupBootstrapRunner");
                logger.LogCritical(
                    "🚨 Application startup halted: {Count} pending migrations detected. Please run 'dotnet ef database update'. Pending: {Migrations}",
                    pendingMigrations.Count(),
                    string.Join(", ", pendingMigrations));

                throw new InvalidOperationException(
                    $"Database schema drift detected. Application cannot start with {pendingMigrations.Count()} pending migrations.");
            }

            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await RoleSeedData.SeedRolesAsync(roleManager);
            await UserSeedData.SeedUsersAsync(userManager);
            await AddDemoData.AddDemoDataAsync();

            context = serviceProvider.GetRequiredService<AppDbContext>();
            await SeedData.SeedProductsAsync(context);
            await CustomerSeedData.SeedGuestCustomerAsync(context);

            Console.WriteLine("Database seeding completed successfully");
        }
    }
}
