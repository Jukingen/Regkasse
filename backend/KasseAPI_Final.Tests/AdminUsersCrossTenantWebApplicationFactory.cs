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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Full HTTP pipeline host for AdminUsers cross-tenant mutation isolation tests.
/// </summary>
public sealed class AdminUsersCrossTenantWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string TenantASlug = "tenant-a";
    internal const string TenantBSlug = "tenant-b";
    internal const string AdminAEmail = "admin-a@test.com";
    internal const string AdminAPassword = "TestPass123!";
    internal const string UserAId = "user-tenant-a";
    internal const string UserBId = "user-tenant-b";
    internal const string JwtIssuer = "OpenApiExport";
    internal const string JwtAudience = "OpenApiExport";
    private static readonly string JwtSecretKey = new string('x', 32);

    internal static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string _databaseName = $"AdminUsersCrossTenant_{Guid.NewGuid():N}";

    public AdminUsersCrossTenantWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, "true");
        Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, _databaseName);
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
        foreach (var role in new[] { Roles.SuperAdmin, Roles.Manager, Roles.Cashier })
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var adminA = new ApplicationUser
        {
            Id = "admin-a",
            UserName = AdminAEmail,
            Email = AdminAEmail,
            FirstName = "Admin",
            LastName = "Tenant A",
            Role = Roles.SuperAdmin,
            EmployeeNumber = "ADM-A-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
        };
        await userManager.CreateAsync(adminA, AdminAPassword).ConfigureAwait(false);
        await userManager.AddToRoleAsync(adminA, Roles.SuperAdmin).ConfigureAwait(false);

        var userA = new ApplicationUser
        {
            Id = UserAId,
            UserName = "cashier-a",
            Email = "cashier-a@test.com",
            FirstName = "Cashier",
            LastName = "Tenant A",
            Role = Roles.Cashier,
            EmployeeNumber = "CSH-A-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
            ConcurrencyStamp = "etag-user-a",
        };
        await userManager.CreateAsync(userA, "OtherPass123!").ConfigureAwait(false);
        await userManager.AddToRoleAsync(userA, Roles.Cashier).ConfigureAwait(false);

        var userB = new ApplicationUser
        {
            Id = UserBId,
            UserName = "cashier-b",
            Email = "cashier-b@test.com",
            FirstName = "Cashier",
            LastName = "Tenant B",
            Role = Roles.Cashier,
            EmployeeNumber = "CSH-B-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
            ConcurrencyStamp = "etag-user-b",
        };
        await userManager.CreateAsync(userB, "OtherPass123!").ConfigureAwait(false);
        await userManager.AddToRoleAsync(userB, Roles.Cashier).ConfigureAwait(false);

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership
            {
                UserId = adminA.Id,
                TenantId = TenantAId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = now,
            },
            new UserTenantMembership
            {
                UserId = userA.Id,
                TenantId = TenantAId,
                IsActive = true,
                CreatedAtUtc = now,
            },
            new UserTenantMembership
            {
                UserId = userB.Id,
                TenantId = TenantBId,
                IsActive = true,
                CreatedAtUtc = now,
            });

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public HttpClient CreateTenantClient(string tenantSlug)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{tenantSlug}.regkasse.local"),
            AllowAutoRedirect = false,
        });
    }
}
