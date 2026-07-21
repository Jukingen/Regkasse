using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Cache;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Full pipeline host for tenant isolation integration tests (middleware + EF fail-closed).
/// </summary>
public sealed class TenantIsolationWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string SuspendedTenantSlug = "suspended";
    internal const string ActiveTenantSlug = "activeco";
    internal const string DevTenantHeaderSlug = "dev";
    internal const string SecretProductName = "SECRET-LEAK-PRODUCT";
    internal const string SuperAdminEmail = "super@test.com";
    internal const string SuperAdminPassword = "TestPass123!";
    internal const string JwtIssuer = "OpenApiExport";
    internal const string JwtAudience = "OpenApiExport";
    private static readonly string JwtSecretKey = new string('x', 32);

    internal static readonly Guid SuspendedTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    internal static readonly Guid ActiveTenantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    internal static readonly Guid CafeTenantId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private readonly string _databaseName = $"TenantIsolation_{Guid.NewGuid():N}";
    private readonly string? _previousOpenApiExportFlag;
    private readonly string? _previousInMemoryDbName;

    public TenantIsolationWebApplicationFactory()
    {
        _previousOpenApiExportFlag = Environment.GetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName);
        _previousInMemoryDbName = Environment.GetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable);
        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, "true");
        Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, _databaseName);
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, null);
        Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, null);
        base.Dispose(disposing);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unused;Username=u;Password=p",
                ["JwtSettings:SecretKey"] = JwtSecretKey,
                ["JwtSettings:Issuer"] = JwtIssuer,
                ["JwtSettings:Audience"] = JwtAudience,
                ["Cors:AllowedOrigins:0"] = "https://test.local",
                ["NtpSettings:Enabled"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtSecretKey));
                options.TokenValidationParameters.ValidIssuer = JwtIssuer;
                options.TokenValidationParameters.ValidAudience = JwtAudience;
            });

            // Avoid Redis Connect during isolation tests (products path uses ICacheService).
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ICacheService>();
            services.AddScoped<ICacheService, MemoryCacheService>();

            ReplaceWithInMemoryDatabase(services);
        });
    }

    private static void ReplaceWithInMemoryDatabase(IServiceCollection services)
    {
        // ApplicationHost already selects InMemory when REGKASSE_TEST_INMEMORY_DB is set; re-register for test isolation.
        var descriptors = services
            .Where(d =>
                d.ServiceType == typeof(AppDbContext)
                || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || d.ServiceType == typeof(IDbContextFactory<AppDbContext>))
            .ToList();

        foreach (var descriptor in descriptors)
            services.Remove(descriptor);

        var databaseName = Environment.GetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable)
            ?? $"TenantIsolation_{Guid.NewGuid():N}";

        services.AddDbContext<AppDbContext>((_, options) =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);

        services.AddDbContextFactory<AppDbContext>((_, options) =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }, ServiceLifetime.Scoped);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        SeedIntegrationDataAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        return host;
    }

    internal static async Task SeedIntegrationDataAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (await db.Tenants.AnyAsync().ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        db.Tenants.AddRange(
            new Tenant
            {
                Id = LegacyDefaultTenantIds.Primary,
                Name = "Default Legacy",
                Slug = LegacyDefaultTenantIds.PrimarySlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                LicenseValidUntilUtc = now.AddYears(1),
            },
            new Tenant
            {
                Id = SuspendedTenantId,
                Name = "Suspended Tenant",
                Slug = SuspendedTenantSlug,
                Status = TenantStatuses.Suspended,
                IsActive = false,
                CreatedAt = now,
            },
            new Tenant
            {
                Id = ActiveTenantId,
                Name = "Active Company",
                Slug = ActiveTenantSlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                LicenseValidUntilUtc = now.AddYears(1),
            },
            new Tenant
            {
                Id = CafeTenantId,
                Name = "Cafe",
                Slug = DevTenantHeaderSlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                LicenseValidUntilUtc = now.AddYears(1),
            });

        var activeCategoryId = Guid.NewGuid();
        var cafeCategoryId = Guid.NewGuid();
        db.Categories.AddRange(
            new Category
            {
                Id = activeCategoryId,
                TenantId = ActiveTenantId,
                Name = "Active Cat",
                VatRate = 10m,
            },
            new Category
            {
                Id = cafeCategoryId,
                TenantId = CafeTenantId,
                Name = "Cafe Cat",
                VatRate = 10m,
            });

        db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = ActiveTenantId,
                Name = SecretProductName,
                Price = 12.34m,
                CategoryId = activeCategoryId,
                Category = "Active Cat",
                StockQuantity = 5,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Standard,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
                Barcode = "leak-test-barcode",
                IsActive = true,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = CafeTenantId,
                Name = "Cafe Latte",
                Price = 3.50m,
                CategoryId = cafeCategoryId,
                Category = "Cafe Cat",
                StockQuantity = 10,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Standard,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
                Barcode = "cafe-latte",
                IsActive = true,
            });

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin).ConfigureAwait(false))
            await roleManager.CreateAsync(new IdentityRole(Roles.SuperAdmin)).ConfigureAwait(false);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdmin = new ApplicationUser
        {
            Id = "super-admin",
            UserName = SuperAdminEmail,
            Email = SuperAdminEmail,
            FirstName = "Super",
            LastName = "Admin",
            Role = Roles.SuperAdmin,
            EmployeeNumber = "SA-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
            MustChangePasswordOnNextLogin = false,
        };
        await userManager.CreateAsync(superAdmin, SuperAdminPassword).ConfigureAwait(false);
        await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin).ConfigureAwait(false);

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership
            {
                UserId = superAdmin.Id,
                TenantId = LegacyDefaultTenantIds.Primary,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = now,
            },
            new UserTenantMembership
            {
                UserId = superAdmin.Id,
                TenantId = ActiveTenantId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = now,
            },
            new UserTenantMembership
            {
                UserId = superAdmin.Id,
                TenantId = CafeTenantId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = now,
            });

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public HttpClient CreateClientForActiveTenantHost()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{ActiveTenantSlug}.regkasse.at"),
            AllowAutoRedirect = false,
        });
    }

    public HttpClient CreateClientForSuspendedTenantHost()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{SuspendedTenantSlug}.regkasse.at"),
            AllowAutoRedirect = false,
        });
    }
}
