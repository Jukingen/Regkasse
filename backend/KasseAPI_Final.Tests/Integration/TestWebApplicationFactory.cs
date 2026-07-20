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
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Tests.Integration;

/// <summary>
/// Minimal HTTP pipeline host for Auth login integration tests (in-memory DB + seeded SuperAdmin).
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string AdminEmail = "admin@admin.com";
    internal const string AdminPassword = "Admin123!";
    internal const string JwtIssuer = "OpenApiExport";
    internal const string JwtAudience = "OpenApiExport";

    private static readonly string JwtSecretKey = new string('x', 32);
    private static readonly Guid TenantId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    private readonly string _databaseName = $"AuthIntegration_{Guid.NewGuid():N}";

    public TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, "true");
        Environment.SetEnvironmentVariable(
            OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable,
            _databaseName);
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
                ["Auth:AllowLegacyLoginWithoutClientApp"] = "false",
                ["Auth:RequireTenantMembershipForLogin"] = "true",
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
        SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        return host;
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (await db.Users.AnyAsync().ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Auth Test Tenant",
            Slug = "auth-test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = now,
            LicenseValidUntilUtc = now.AddYears(1),
        });

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin).ConfigureAwait(false))
            await roleManager.CreateAsync(new IdentityRole(Roles.SuperAdmin)).ConfigureAwait(false);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser
        {
            Id = "auth-integration-admin",
            UserName = AdminEmail,
            Email = AdminEmail,
            FirstName = "Auth",
            LastName = "Admin",
            Role = Roles.SuperAdmin,
            EmployeeNumber = "AUTH-001",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
            MustChangePasswordOnNextLogin = false,
        };

        var create = await userManager.CreateAsync(admin, AdminPassword).ConfigureAwait(false);
        if (!create.Succeeded)
            throw new InvalidOperationException(
                "Auth integration seed failed: " + string.Join("; ", create.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, Roles.SuperAdmin).ConfigureAwait(false);

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = admin.Id,
            TenantId = TenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
