using System.Text;
using KasseAPI_Final;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP host for backup artifact import integration tests (Manager + backup.manage, tenant-scoped).
/// </summary>
public sealed class BackupImportWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string TenantASlug = "tenant-a";
    internal const string TenantBSlug = "tenant-b";
    internal const string ManagerEmail = "manager-a@test.com";
    internal const string ManagerPassword = "TestPass123!";
    internal const string JwtIssuer = "OpenApiExport";
    internal const string JwtAudience = "OpenApiExport";
    private static readonly string JwtSecretKey = new string('x', 32);

    internal static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string _databaseName = $"BackupImport_{Guid.NewGuid():N}";
    private readonly string _stagingRoot;

    public BackupImportWebApplicationFactory()
    {
        _stagingRoot = Path.Combine(Path.GetTempPath(), "bk_import_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_stagingRoot);

        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, "true");
        Environment.SetEnvironmentVariable(
            OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable,
            _databaseName);
    }

    public string StagingRoot => _stagingRoot;

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
                ["Backup:ArtifactStagingRoot"] = _stagingRoot,
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
        });
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

        if (await db.Tenants.IgnoreQueryFilters().AnyAsync().ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        db.Tenants.AddRange(
            new Tenant
            {
                Id = TenantAId,
                Name = "Tenant A",
                Slug = TenantASlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                LicenseValidUntilUtc = now.AddYears(1),
            },
            new Tenant
            {
                Id = TenantBId,
                Name = "Tenant B",
                Slug = TenantBSlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                LicenseValidUntilUtc = now.AddYears(1),
            });

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.Manager).ConfigureAwait(false))
            await roleManager.CreateAsync(new IdentityRole(Roles.Manager)).ConfigureAwait(false);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var manager = new ApplicationUser
        {
            Id = "manager-a",
            UserName = ManagerEmail,
            Email = ManagerEmail,
            FirstName = "Manager",
            LastName = "Tenant A",
            Role = Roles.Manager,
            EmployeeNumber = "MGR-A-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
        };
        await userManager.CreateAsync(manager, ManagerPassword).ConfigureAwait(false);
        await userManager.AddToRoleAsync(manager, Roles.Manager).ConfigureAwait(false);

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = manager.Id,
            TenantId = TenantAId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public HttpClient CreateTenantClient(string tenantSlug) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{tenantSlug}.regkasse.local"),
            AllowAutoRedirect = false,
        });
}
