using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final
{
    /// <summary>
    /// Orchestrates all startup bootstrap responsibilities: database migration,
    /// pending migration gate, role/user seeding, demo data, product seed, guest customer seed,
        /// and (Development only) default cash registers for active tenants that do not yet have one.
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

            // Fail-fast: auth session/refresh schema must exist before login pipeline can issue tokens.
            await VerifyCriticalAuthSchemaAsync(context);

            await VerifyCriticalCatalogSchemaAsync(context);

            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await RoleSeedData.SeedRolesAsync(roleManager);
            var tenantMembershipProvisioner = serviceProvider.GetRequiredService<IUserTenantMembershipProvisioner>();
            await UserSeedData.SeedUsersAsync(userManager, tenantMembershipProvisioner);
            var webEnv = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            await DemoTenantAdminSeed.SeedAsync(context, userManager, tenantMembershipProvisioner, webEnv);
            await AddDemoData.AddDemoDataAsync(context);

            context = serviceProvider.GetRequiredService<AppDbContext>();
            await SeedData.SeedProductsAsync(context);
            await CustomerSeedData.SeedGuestCustomerAsync(context);

            if (webEnv.IsDevelopment())
            {
                var cashRegisterLogger = serviceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("KasseAPI_Final.Data.CashRegisterBootstrapSeed");
                await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
                    context,
                    cashRegisterLogger);
            }

            Console.WriteLine("Database seeding completed successfully");
        }

        private static async Task VerifyCriticalAuthSchemaAsync(AppDbContext context)
        {
            static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "select to_regclass(@t) is not null";
                var p = cmd.CreateParameter();
                p.ParameterName = "@t";
                p.Value = tableName;
                cmd.Parameters.Add(p);
                var result = await cmd.ExecuteScalarAsync();
                return result is bool b && b;
            }

            var hasAuthSessions = await TableExistsAsync(context, "public.auth_sessions");
            var hasRefreshTokens = await TableExistsAsync(context, "public.refresh_tokens");

            if (hasAuthSessions && hasRefreshTokens)
                return;

            var conn = context.Database.GetDbConnection().ConnectionString ?? "(empty)";
            var safeConn = System.Text.RegularExpressions.Regex.Replace(conn, @"Password=[^;]*", "Password=***");
            throw new InvalidOperationException(
                $"Critical auth schema missing. auth_sessions={hasAuthSessions}, refresh_tokens={hasRefreshTokens}. " +
                $"Connection={safeConn}. Ensure migration 'AddAuthSessionsAndRefreshTokens' is applied to the active database.");
        }

        /// <summary>
        /// Ensures recent catalog migrations were applied to the same database the API uses (demo import / admin products).
        /// </summary>
        private static async Task VerifyCriticalCatalogSchemaAsync(AppDbContext context)
        {
            static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = @table
                          AND column_name = @column
                    )
                    """;
                var tableParam = cmd.CreateParameter();
                tableParam.ParameterName = "@table";
                tableParam.Value = table;
                cmd.Parameters.Add(tableParam);
                var columnParam = cmd.CreateParameter();
                columnParam.ParameterName = "@column";
                columnParam.Value = column;
                cmd.Parameters.Add(columnParam);
                var result = await cmd.ExecuteScalarAsync();
                return result is bool b && b;
            }

            var hasCategoryFiscal = await ColumnExistsAsync(context, "categories", "fiscal_category");
            var hasCategoryKey = await ColumnExistsAsync(context, "categories", "category_key");
            var hasProductNameDe = await ColumnExistsAsync(context, "products", "name_de");

            if (hasCategoryFiscal && hasCategoryKey && hasProductNameDe)
                return;

            var conn = context.Database.GetDbConnection().ConnectionString ?? "(empty)";
            var safeConn = System.Text.RegularExpressions.Regex.Replace(conn, @"Password=[^;]*", "Password=***");
            throw new InvalidOperationException(
                "Critical catalog schema missing. " +
                $"categories.fiscal_category={hasCategoryFiscal}, categories.category_key={hasCategoryKey}, products.name_de={hasProductNameDe}. " +
                $"Connection={safeConn}. From the backend folder run: dotnet ef database update --project KasseAPI_Final.csproj");
        }
    }
}
