using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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
/// HTTP pipeline host for Manager admin oversight integration tests (payment.view, sale.view, report.export).
/// </summary>
public sealed class ManagerOversightWebApplicationFactory : WebApplicationFactory<Program>
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
    internal static readonly Guid CashRegisterAId = Guid.Parse("ca000001-0001-0001-0001-000000000001");
    internal static readonly Guid CashRegisterBId = Guid.Parse("cb000002-0002-0002-0002-000000000002");
    internal static readonly Guid CustomerAId = Guid.Parse("c0a00001-0001-0001-0001-000000000001");
    internal static readonly Guid CustomerBId = Guid.Parse("c0b00002-0002-0002-0002-000000000002");
    internal static readonly Guid PaymentAId = Guid.Parse("f0a00001-0001-0001-0001-000000000001");
    internal static readonly Guid PaymentBId = Guid.Parse("f0b00002-0002-0002-0002-000000000002");

    private readonly string _databaseName = $"ManagerOversight_{Guid.NewGuid():N}";
    private readonly string? _previousOpenApiExportFlag;
    private readonly string? _previousInMemoryDbName;

    public ManagerOversightWebApplicationFactory()
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
        foreach (var role in new[] { Roles.Manager, Roles.Cashier })
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
        }

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

        db.Customers.AddRange(
            new Customer
            {
                Id = CustomerAId,
                Name = "Customer A",
                Email = "cust-a@test.com",
                Phone = "1",
                IsActive = true,
            },
            new Customer
            {
                Id = CustomerBId,
                Name = "Customer B",
                Email = "cust-b@test.com",
                Phone = "2",
                IsActive = true,
            });

        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = CashRegisterAId,
                TenantId = TenantAId,
                RegisterNumber = "KA-01",
                Location = "Floor A",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Open,
                CreatedAt = now,
                IsActive = true,
            },
            new CashRegister
            {
                Id = CashRegisterBId,
                TenantId = TenantBId,
                RegisterNumber = "KB-01",
                Location = "Floor B",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Open,
                CreatedAt = now,
                IsActive = true,
            });

        db.PaymentDetails.AddRange(
            CreatePayment(PaymentAId, CustomerAId, CashRegisterAId, now),
            CreatePayment(PaymentBId, CustomerBId, CashRegisterBId, now));

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static PaymentDetails CreatePayment(Guid id, Guid customerId, Guid cashRegisterId, DateTime createdAt) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            ReceiptNumber = $"R-{id:N}".Substring(0, 20),
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsActive = true,
        };

    public HttpClient CreateTenantClient(string tenantSlug) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{tenantSlug}.regkasse.local"),
            AllowAutoRedirect = false,
        });
}
