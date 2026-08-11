using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantsControllerTests
{
    private static readonly ConditionalWeakTable<AppDbContext, IServiceScopeFactory> DbScopeFactories = new();

    private static AppDbContext CreateDb(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var (db, scopeFactory) = CreateDbWithScopeFactory(tenantAccessor);
        DbScopeFactories.Add(db, scopeFactory);
        return db;
    }

    private static (AppDbContext Db, IServiceScopeFactory ScopeFactory) CreateDbWithScopeFactory(
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        var dbName = $"AdminTenants_{Guid.NewGuid():N}";
        var accessor = tenantAccessor ?? NullCurrentTenantAccessor.Instance;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, accessor);

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenantAccessor>(accessor);
        services.AddDbContextFactory<AppDbContext>(builder =>
            builder
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return (db, scopeFactory);
    }

    private static IServiceScopeFactory CreateScopeFactoryForDb(AppDbContext db)
    {
        if (DbScopeFactories.TryGetValue(db, out var scopeFactory))
            return scopeFactory;

        throw new InvalidOperationException("CreateDb() must be used before wiring TenantDeletionService for tests.");
    }

    private static UserManager<ApplicationUser> CreateUserManagerStub()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static ITenantOnboardingService CreateOnboardingService(
        AppDbContext db,
        ITenantProvisioningService? provisioning = null)
    {
        var provisioningMock = provisioning ?? CreateSuccessfulProvisioningMock();
        var checklist = new Mock<KasseAPI_Final.Services.Onboarding.ITenantOnboardingChecklistService>();
        checklist
            .Setup(c => c.EnsureAndGetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KasseAPI_Final.Services.Onboarding.TenantOnboardingOverviewDto());

        return new TenantOnboardingService(
            db,
            provisioningMock,
            Mock.Of<IWelcomeEmailService>(),
            Mock.Of<IAuditLogService>(),
            checklist.Object,
            Mock.Of<ILogger<TenantOnboardingService>>());
    }

    private static ITenantHardDeletePolicy CreateHardDeletePolicy(
        IHostEnvironment? environment = null,
        TenantDeletionOptions? options = null) =>
        new TenantHardDeletePolicy(
            environment ?? Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            Options.Create(options ?? new TenantDeletionOptions()));

    private static ITenantDeletionService CreateTenantDeletionService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment? environment = null,
        TenantDeletionOptions? options = null,
        ITenantHardDeletePolicy? policy = null)
    {
        return new TenantDeletionService(
            scopeFactory,
            policy ?? CreateHardDeletePolicy(environment, options));
    }

    private static ITenantDeletionService CreateTenantDeletionService(
        AppDbContext db,
        IAuditLogService? auditLog = null,
        IHostEnvironment? environment = null,
        TenantDeletionOptions? options = null,
        ITenantHardDeletePolicy? policy = null)
    {
        _ = auditLog;
        return CreateTenantDeletionService(
            CreateScopeFactoryForDb(db),
            environment,
            options,
            policy);
    }

    private static AdminTenantService CreateService(
        AppDbContext db,
        ITenantProvisioningService? provisioning = null,
        IAuditLogService? auditLog = null,
        ICashRegisterDecommissionService? decommissionService = null,
        IHttpContextAccessor? httpContextAccessor = null,
        ICurrentTenantAccessor? tenantAccessor = null,
        IHostEnvironment? environment = null,
        TenantDeletionOptions? deletionOptions = null)
    {
        var audit = auditLog ?? Mock.Of<IAuditLogService>();
        var tenantDeletion = CreateTenantDeletionService(db, audit, environment, deletionOptions);
        var tenantLifecycle = new TenantService(db, audit, tenantDeletion, Mock.Of<ILogger<TenantService>>());
        var tenantScopeAccessor = tenantAccessor ?? NullCurrentTenantAccessor.Instance;
        var accessor = httpContextAccessor ?? CreateHttpContextAccessor();
        return new AdminTenantService(
            db,
            CreateUserManagerStub(),
            Mock.Of<ITokenClaimsService>(),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IJwtAccessTokenIssuer>(),
            Options.Create(new AuthOptions()),
            CreateOnboardingService(db, provisioning),
            tenantLifecycle,
            tenantDeletion,
            decommissionService ?? Mock.Of<ICashRegisterDecommissionService>(),
            accessor,
            tenantScopeAccessor,
            Mock.Of<ILogger<AdminTenantService>>());
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(ClaimsPrincipal? user = null)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        },
                        authenticationType: "TestAuth"))
            }
        };

        return accessor;
    }

    private static AdminTenantsController CreateController(
        IAdminTenantService? tenantService = null,
        IAdminTenantLicenseService? tenantLicenseService = null,
        ITenantDeletionService? tenantDeletionService = null,
        IHostEnvironment? environment = null,
        ClaimsPrincipal? user = null)
    {
        var controller = new AdminTenantsController(
            tenantService ?? Mock.Of<IAdminTenantService>(),
            Mock.Of<IAdminTenantCsvExportService>(),
            tenantLicenseService ?? Mock.Of<IAdminTenantLicenseService>(),
            tenantDeletionService ?? Mock.Of<ITenantDeletionService>(),
            Mock.Of<KasseAPI_Final.Services.ActivityReports.IActivityReportService>(),
            Mock.Of<IAuditLogService>(),
            environment ?? Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            Mock.Of<ILogger<AdminTenantsController>>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        },
                        authenticationType: "TestAuth"))
            }
        };

        return controller;
    }

    private static AdminTenantsController CreateFullController(
        AppDbContext db,
        IHostEnvironment? environment = null,
        IAuditLogService? auditLog = null)
    {
        var service = CreateService(db, auditLog: auditLog, environment: environment);
        var tenantDeletion = CreateTenantDeletionService(db, environment: environment);
        return CreateController(
            tenantService: service,
            tenantDeletionService: tenantDeletion,
            environment: environment);
    }

    private static ITenantProvisioningService CreateSuccessfulProvisioningMock()
    {
        var mock = new Mock<ITenantProvisioningService>();
        mock.Setup(p => p.ProvisionAsync(
                It.IsAny<Tenant>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, string? _, string? __, bool grantTrial, bool _, string? ___, bool ____, CancellationToken _) =>
            {
                if (grantTrial)
                    t.LicenseValidUntilUtc = DateTime.UtcNow.AddDays(30);
                return (new TenantProvisioningResult
                {
                    CashRegisterId = Guid.NewGuid(),
                    CashRegisterNumber = "KASSE-001",
                    AdminUserId = "admin-id",
                    AdminEmail = $"admin@{t.Slug}.regkasse.at",
                    GeneratedPassword = "TestPass1!",
                    CategoryId = Guid.NewGuid(),
                    ProductIds = new List<Guid> { Guid.NewGuid() },
                }, null);
            });
        return mock.Object;
    }

    [Fact]
    public async Task CheckSlugAvailabilityAsync_ReturnsTakenWhenExists()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "cafe-example",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckSlugAvailabilityAsync("cafe-example");

        Assert.True(result.IsValid);
        Assert.False(result.Available);
        Assert.Equal("cafe-example", result.NormalizedSlug);
    }

    [Fact]
    public async Task CreateAsync_WhenSlugTaken_ReturnsSuggestions()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "dev",
            Slug = "cafe-beispiel",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (_, failure) = await service.CreateWithFailureDetailAsync(
            new CreateAdminTenantRequest { Name = "Café Beispiel", Slug = "cafe-beispiel", Email = "owner@test.at" },
            "actor-1");

        Assert.NotNull(failure);
        Assert.Equal(TenantOnboardingErrorCodes.SlugTaken, failure!.Code);
        Assert.NotEmpty(failure.SlugSuggestions ?? Array.Empty<string>());
        Assert.DoesNotContain("cafe-beispiel", failure.SlugSuggestions!, StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetSlugSuggestionsAsync_ExcludesTakenSlugs()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Taken",
            Slug = "cafe-beispiel",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var suggestions = await service.GetSlugSuggestionsAsync("Café Beispiel", "cafe-beispiel");

        Assert.NotEmpty(suggestions);
        Assert.DoesNotContain("cafe-beispiel", suggestions, StringComparer.Ordinal);
    }

    [Fact]
    public async Task CheckSlugAvailabilityAsync_ReturnsAvailableForNewSlug()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var result = await service.CheckSlugAvailabilityAsync("new-cafe");

        Assert.True(result.IsValid);
        Assert.True(result.Available);
        Assert.Equal("new-cafe", result.NormalizedSlug);
    }

    [Fact]
    public async Task CreateAsync_PersistsTenant_WithSlug()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var (result, error) = await service.CreateAsync(
            new CreateAdminTenantRequest { Name = "Acme Demo", Slug = "acme-demo" },
            "actor-1");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("acme-demo", result!.Slug);
        Assert.Equal(TenantStatuses.Active, result.Status);
        Assert.NotNull(result.Provisioning);
        Assert.Equal("KASSE-001", result.Provisioning!.CashRegisterNumber);
    }

    [Fact]
    public async Task SoftDeleteAsync_ValidTenant_SetsStatusDeleted()
    {
        await using var db = CreateDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Temp",
            Slug = "temp_tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (success, error) = await service.SoftDeleteAsync(tenant.Id, "actor-1");

        Assert.True(success);
        Assert.Null(error);
        var row = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(TenantStatuses.Cancelled, row.Status);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task HardDeleteDevelopment_WhenNotDevelopment_ReturnsBadRequest()
    {
        var service = new Mock<IAdminTenantService>(MockBehavior.Strict);
        var environment = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var controller = CreateController(service.Object, environment: environment);

        var result = await controller.HardDeleteDevelopment(Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task HardDeleteDevelopment_WhenDevelopment_SoftDeletesThenHardDeletes()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new AdminTenantDetailDto(
            tenantId,
            "Dev Tenant",
            "dev-tenant",
            null,
            null,
            null,
            TenantStatuses.Active,
            true,
            null,
            null,
            DateTime.UtcNow,
            null,
            null);

        var sequence = new MockSequence();
        var service = new Mock<IAdminTenantService>(MockBehavior.Strict);
        service.InSequence(sequence)
            .Setup(s => s.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        service.InSequence(sequence)
            .Setup(s => s.SoftDeleteAsync(tenantId, "super-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null));
        service.InSequence(sequence)
            .Setup(s => s.HardDeleteAsync(
                tenantId,
                It.Is<HardDeleteAdminTenantRequest>(r => r.ConfirmSlug == "dev-tenant"),
                "super-admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermanentDeleteResult(Success: true));

        var controller = CreateController(service.Object);

        var result = await controller.HardDeleteDevelopment(tenantId);

        Assert.IsType<NoContentResult>(result);
        service.VerifyAll();
    }

    [Fact]
    public async Task GetDecommissionChecksAsync_ReturnsExpectedFlagsAndCounts()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Temp",
            Slug = "temp_tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var closedRegisterId = Guid.NewGuid();
        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = closedRegisterId,
                TenantId = tenantId,
                RegisterNumber = "KASSE-001",
                Location = "Front",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegisterNumber = "KASSE-002",
                Location = "Back",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegisterNumber = "KASSE-003",
                Location = "Archive",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Decommissioned,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        db.PaymentDetails.Add(CreatePendingPayment(closedRegisterId));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var checks = await service.GetDecommissionChecksAsync(tenantId);

        Assert.NotNull(checks);
        Assert.True(checks!.HasOpenPayments);
        Assert.True(checks.HasOpenShifts);
        Assert.Equal(2, checks.ActiveRegistersCount);
        Assert.Equal(1, checks.ReadyRegistersCount);
        Assert.Equal(1, checks.BlockedRegistersCount);
        Assert.False(checks.CanDecommission);
    }

    [Fact]
    public async Task DecommissionAsync_UsesTenantScopeForRegisters_AndSoftDeletesTenant()
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary };
        await using var db = CreateDb(tenantAccessor);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Temp",
            Slug = "temp_tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var registerIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var (registerId, index) in registerIds.Select((value, idx) => (value, idx)))
        {
            db.CashRegisters.Add(new CashRegister
            {
                Id = registerId,
                TenantId = tenantId,
                RegisterNumber = $"KASSE-00{index + 1}",
                Location = "Front",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        var httpContextAccessor = CreateHttpContextAccessor();
        var decommissionMock = new Mock<ICashRegisterDecommissionService>();
        decommissionMock
            .Setup(s => s.DecommissionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Guid, string?, string, string, CancellationToken>((registerId, _, _, _, _) =>
            {
                Assert.Equal(tenantId, tenantAccessor.TenantId);
                Assert.Equal(
                    tenantId.ToString("D"),
                    httpContextAccessor.HttpContext!.User.FindFirst(ScopeCheckService.TenantIdClaim)?.Value);

                return Task.FromResult(new DecommissionCashRegisterResponse
                {
                    CashRegisterId = registerId,
                    PaymentId = Guid.NewGuid(),
                    ReceiptId = Guid.NewGuid(),
                    ReceiptNumber = $"R-{registerId:N}",
                    Message = "ok",
                });
            });

        var service = CreateService(
            db,
            decommissionService: decommissionMock.Object,
            httpContextAccessor: httpContextAccessor,
            tenantAccessor: tenantAccessor);

        var (success, error, checks) = await service.DecommissionAsync(
            tenantId,
            "actor-1",
            Roles.SuperAdmin);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(checks);
        Assert.True(checks!.CanDecommission);
        decommissionMock.Verify(
            s => s.DecommissionAsync(
                It.IsAny<Guid>(),
                "Tenant decommission",
                "actor-1",
                Roles.SuperAdmin,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Equal(LegacyDefaultTenantIds.Primary, tenantAccessor.TenantId);
        Assert.Null(httpContextAccessor.HttpContext!.User.FindFirst(ScopeCheckService.TenantIdClaim));

        var tenantRow = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatuses.Cancelled, tenantRow.Status);
        Assert.False(tenantRow.IsActive);
    }

    [Fact]
    public async Task ListAsync_Includes_Owner_Admin_And_Demo_Preset_Flags()
    {
        await using var db = CreateDb();
        var prodId = DemoTenantIds.Prod;
        var devExampleId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = prodId,
            Name = "Production",
            Slug = "prod",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = devExampleId,
            Name = "Development Example",
            Slug = "dev-example",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "owner-prod",
            TenantId = prodId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-prod",
            UserName = "admin@prod.regkasse.at",
            Email = "admin@prod.regkasse.at",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListAsync(false);

        var prod = list.Single(x => x.Slug == "prod");
        Assert.Equal("admin@prod.regkasse.at", prod.OwnerAdminEmail);
        Assert.False(prod.IsDemoPreset);

        var devExample = list.Single(x => x.Slug == "dev-example");
        Assert.Null(devExample.OwnerAdminEmail);
        Assert.False(devExample.IsDemoPreset);

        var dev = list.Single(x => x.Slug == "dev");
        Assert.True(dev.IsDemoPreset);
    }

    [Fact]
    public async Task ListAsync_Enriches_Aggregates_From_Related_Tables()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var saleUntil = now.AddDays(20);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Aggregate Cafe",
            Slug = "aggregate-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = "REGK-AGG-KEY",
            LicenseValidUntilUtc = now.AddDays(5),
            CreatedAt = now.AddDays(-10),
        });

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-agg",
                UserName = "owner@aggregate.test",
                Email = "owner@aggregate.test",
                FirstName = "Owner",
                LastName = "Agg",
                Role = Roles.Manager,
                IsActive = true,
                EmailConfirmed = true,
            },
            new ApplicationUser
            {
                Id = "cashier-agg",
                UserName = "cashier@aggregate.test",
                Email = "cashier@aggregate.test",
                FirstName = "Cash",
                LastName = "Agg",
                Role = Roles.Cashier,
                IsActive = true,
                EmailConfirmed = true,
            });

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership
            {
                UserId = "owner-agg",
                TenantId = tenantId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = now,
            },
            new UserTenantMembership
            {
                UserId = "cashier-agg",
                TenantId = tenantId,
                IsActive = true,
                IsOwner = false,
                CreatedAtUtc = now,
            });

        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegisterNumber = "K1",
                Location = "Front",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now,
                IsActive = true,
            },
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegisterNumber = "K2",
                Location = "Bar",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now,
                IsActive = true,
            });

        db.LicenseSales.Add(new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = "REGK-AGG-SALE",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            LicenseType = LicenseType.Business,
            ValidFromUtc = now.AddDays(-10),
            ValidUntilUtc = saleUntil,
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            Currency = "EUR",
            SoldAtUtc = now.AddDays(-10),
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = "RE202608AGG01",
            Status = LicenseSaleStatuses.Active,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-10),
        });

        var olderAudit = now.AddDays(-2);
        var newerAudit = now.AddHours(-1);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = "s1",
                UserId = "owner-agg",
                UserRole = Roles.Manager,
                Action = "LOGIN",
                EntityType = "User",
                Status = AuditLogStatus.Success,
                Timestamp = olderAudit,
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = "s2",
                UserId = "cashier-agg",
                UserRole = Roles.Cashier,
                Action = "PAYMENT",
                EntityType = "Payment",
                Status = AuditLogStatus.Success,
                Timestamp = newerAudit,
            });

        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListAsync(false);
        var row = Assert.Single(list, x => x.Id == tenantId);

        Assert.Equal(LicenseType.Business, row.LicenseType);
        Assert.Equal(2, row.RegisterCount);
        Assert.Equal(2, row.UserCount);
        Assert.Equal("owner@aggregate.test", row.OwnerAdminEmail);
        Assert.Equal(newerAudit, row.LastActivityAtUtc);
        Assert.NotNull(row.LicenseDaysRemaining);
        Assert.InRange(row.LicenseDaysRemaining!.Value, 19, 21);
    }

    [Fact]
    public async Task ListPagedAsync_Filters_Sorts_And_Paginates()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;

        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var gammaId = Guid.NewGuid();
        var suspendedId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = alphaId,
                Name = "Alpha Cafe",
                Slug = "alpha-cafe",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseKey = "REGK-ALPHA",
                LicenseValidUntilUtc = now.AddDays(30),
                CreatedAt = now.AddDays(-3),
            },
            new Tenant
            {
                Id = betaId,
                Name = "Beta Bar",
                Slug = "beta-bar",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseKey = null,
                LicenseValidUntilUtc = now.AddDays(14),
                CreatedAt = now.AddDays(-2),
            },
            new Tenant
            {
                Id = gammaId,
                Name = "Gamma Grill",
                Slug = "gamma-grill",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseKey = "REGK-GAMMA",
                LicenseValidUntilUtc = now.AddDays(10),
                CreatedAt = now.AddDays(-1),
            },
            new Tenant
            {
                Id = suspendedId,
                Name = "Suspended Shop",
                Slug = "suspended-shop",
                Status = TenantStatuses.Suspended,
                IsActive = false,
                CreatedAt = now,
            });

        db.LicenseSales.Add(new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = alphaId,
            LicenseKey = "REGK-ALPHA-SALE",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            LicenseType = LicenseType.Business,
            ValidFromUtc = now.AddDays(-30),
            ValidUntilUtc = now.AddDays(30),
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            Currency = "EUR",
            SoldAtUtc = now.AddDays(-30),
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = "RE202608PAGE01",
            Status = LicenseSaleStatuses.Active,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-30),
        });

        db.LicenseSales.Add(new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = gammaId,
            LicenseKey = "REGK-GAMMA-SALE",
            LicensePlan = LicenseSalePlans.SixMonths,
            LicenseType = LicenseType.Starter,
            ValidFromUtc = now.AddDays(-10),
            ValidUntilUtc = now.AddDays(10),
            PriceNet = 80m,
            VatRate = 20m,
            VatAmount = 16m,
            PriceGross = 96m,
            Currency = "EUR",
            SoldAtUtc = now.AddDays(-10),
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = "RE202608PAGE02",
            Status = LicenseSaleStatuses.Active,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-10),
        });

        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = alphaId,
                RegisterNumber = "A1",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now,
                IsActive = true,
            },
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = alphaId,
                RegisterNumber = "A2",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now,
                IsActive = true,
            },
            new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = gammaId,
                RegisterNumber = "G1",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now,
                IsActive = true,
            });

        await db.SaveChangesAsync();
        var service = CreateService(db);

        var byStatus = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Status = "Active",
            SortBy = "Name",
            SortOrder = "Asc",
            Page = 1,
            PageSize = 50,
        });
        Assert.Equal(3, byStatus.TotalCount);
        Assert.DoesNotContain(byStatus.Items, t => t.Id == suspendedId);

        var bySearch = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Search = "beta",
            Page = 1,
            PageSize = 20,
        });
        Assert.Equal(1, bySearch.TotalCount);
        Assert.Equal(betaId, Assert.Single(bySearch.Items).Id);

        var byLicense = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Status = TenantStatuses.Active,
            LicenseType = LicenseType.Trial,
            Page = 1,
            PageSize = 20,
        });
        Assert.Equal(1, byLicense.TotalCount);
        Assert.Equal(betaId, Assert.Single(byLicense.Items).Id);
        Assert.Equal(LicenseType.Trial, byLicense.Items[0].LicenseType);

        var byBusiness = await service.ListPagedAsync(new AdminTenantListQuery
        {
            LicenseType = LicenseType.Business,
            Page = 1,
            PageSize = 20,
        });
        Assert.Equal(1, byBusiness.TotalCount);
        Assert.Equal(alphaId, Assert.Single(byBusiness.Items).Id);

        var sorted = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Status = TenantStatuses.Active,
            SortBy = "RegisterCount",
            SortOrder = "Desc",
            Page = 1,
            PageSize = 20,
        });
        Assert.Equal(3, sorted.Items.Count);
        Assert.Equal(alphaId, sorted.Items[0].Id);
        Assert.Equal(2, sorted.Items[0].RegisterCount);

        var page1 = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Status = TenantStatuses.Active,
            SortBy = "Name",
            SortOrder = "Asc",
            Page = 1,
            PageSize = 2,
        });
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(alphaId, page1.Items[0].Id);
        Assert.Equal(betaId, page1.Items[1].Id);

        var page2 = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Status = TenantStatuses.Active,
            SortBy = "Name",
            SortOrder = "Asc",
            Page = 2,
            PageSize = 2,
        });
        Assert.Equal(1, page2.Items.Count);
        Assert.Equal(gammaId, page2.Items[0].Id);

        var clamped = await service.ListPagedAsync(new AdminTenantListQuery
        {
            Page = 0,
            PageSize = 500,
        });
        Assert.Equal(1, clamped.Page);
        Assert.Equal(100, clamped.PageSize);
    }

    [Fact]
    public async Task ListAsync_Ignores_Ambient_Tenant_Filter_For_Owner_Admin_Email()
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = DemoTenantIds.Dev };
        await using var db = CreateDb(tenantAccessor);
        var barId = Guid.NewGuid();
        var cafeId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = barId,
            Name = "Test Bar",
            Slug = "prod",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = cafeId,
            Name = "Test Cafe",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-bar",
                UserName = "admin@prod.regkasse.at",
                Email = "admin@prod.regkasse.at",
                FirstName = "prod",
                LastName = "Owner",
                Role = Roles.Manager,
                IsActive = true,
                EmailConfirmed = true,
            },
            new ApplicationUser
            {
                Id = "owner-cafe",
                UserName = "admin@dev.regkasse.at",
                Email = "admin@dev.regkasse.at",
                FirstName = "dev",
                LastName = "Owner",
                Role = Roles.Manager,
                IsActive = true,
                EmailConfirmed = true,
            });
        db.UserTenantMemberships.AddRange(
            new UserTenantMembership
            {
                UserId = "owner-bar",
                TenantId = barId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = DateTime.UtcNow,
            },
            new UserTenantMembership
            {
                UserId = "owner-cafe",
                TenantId = cafeId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListAsync(false);

        Assert.Equal("admin@prod.regkasse.at", list.Single(x => x.Slug == "prod").OwnerAdminEmail);
        // Two rows share slug "dev" in this fixture (seeded cafe + DemoTenantIds.Dev); assert owner via tenant id.
        Assert.Equal("admin@dev.regkasse.at", list.Single(x => x.Id == cafeId).OwnerAdminEmail);
    }

    [Fact]
    public async Task ListForSwitcherAsync_Filters_To_Active_Memberships_For_Non_SuperAdmin()
    {
        await using var db = CreateDb();
        var memberTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = memberTenantId,
            Name = "Member Cafe",
            Slug = "member-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other Bar",
            Slug = "other-bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "manager-1",
            TenantId = memberTenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var all = await service.ListForSwitcherAsync("manager-1", actorIsSuperAdmin: false, includeDeleted: false);
        Assert.Single(all);
        Assert.Equal("member-cafe", all[0].Slug);

        var superList = await service.ListForSwitcherAsync("manager-1", actorIsSuperAdmin: true, includeDeleted: false);
        Assert.Equal(2, superList.Count);
        Assert.Equal(superList.Count, superList.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task ListForSwitcherAsync_Excludes_Unused_Default_Tenant()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListForSwitcherAsync("super-1", actorIsSuperAdmin: true, includeDeleted: false);

        Assert.DoesNotContain(list, t => t.Slug == LegacyDefaultTenantIds.PrimarySlug);
        Assert.Contains(list, t => t.Slug == "dev");
    }

    [Fact]
    public async Task ListForSwitcherAsync_SuperAdmin_Returns_Unique_Tenant_Ids_Even_With_Multiple_Owner_Memberships()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dup Guard Cafe",
            Slug = "dup-guard-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-a",
            UserName = "a@test.local",
            Email = "a@test.local",
            FirstName = "A",
            LastName = "A",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-b",
            UserName = "b@test.local",
            Email = "b@test.local",
            FirstName = "B",
            LastName = "B",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "owner-a",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "owner-b",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListForSwitcherAsync("super-1", actorIsSuperAdmin: true, includeDeleted: false);

        var matches = list.Where(x => x.Id == tenantId).ToList();
        Assert.Single(matches);
        Assert.Equal(list.Count, list.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task ListCashRegistersAsync_ReturnsRegisters_ForTenant()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "dev",
            Slug = "cafe-x",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var registers = await service.ListCashRegistersAsync(tenantId);

        Assert.NotNull(registers);
        Assert.Single(registers!);
        Assert.Equal("KASSE-001", registers![0].RegisterNumber);
        Assert.Equal("Open", registers[0].Status);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesSummaryCounts()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Stats",
            Slug = "stats",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var detail = await service.GetByIdAsync(tenantId);

        Assert.NotNull(detail);
        Assert.Equal(1, detail!.ActiveUserCount);
        Assert.Equal(1, detail.CashRegisterCount);
        Assert.NotNull(detail.LastActivityAtUtc);
    }

    [Fact]
    public async Task GetByIdAsync_Ignores_Ambient_Tenant_Filter_For_Owner_Admin_Email()
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = DemoTenantIds.Dev };
        await using var db = CreateDb(tenantAccessor);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "dev",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-1",
            UserName = "admin@dev.regkasse.at",
            Email = "admin@dev.regkasse.at",
            FirstName = "dev",
            LastName = "Owner",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "owner-1",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var detail = await service.GetByIdAsync(tenantId);

        Assert.NotNull(detail);
        Assert.Equal("admin@dev.regkasse.at", detail!.OwnerAdminEmail);
    }

    [Fact]
    public async Task SoftDeleteAsync_LegacyDefaultTenant_ThrowsError()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (success, error) = await service.SoftDeleteAsync(LegacyDefaultTenantIds.Primary, "actor-1");

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task SoftDeleteAsync_Idempotent_WhenAlreadyDeleted()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Gone",
            Slug = "gone-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        var service = CreateService(db, auditLog: audit.Object);
        var (success, error) = await service.SoftDeleteAsync(tenantId, "actor-1");

        Assert.True(success);
        Assert.Null(error);
        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_SOFT_DELETED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>()),
            Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_DeactivatesMemberships_And_WritesAudit()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Off",
            Slug = "cafe-off",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "admin@cafe-off.test",
            Email = "admin@cafe-off.test",
            FirstName = "dev",
            LastName = "Admin",
            Role = Roles.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_SOFT_DELETED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.TENANT_SOFT_DELETED });

        var service = CreateService(db, auditLog: audit.Object);
        var (success, _) = await service.SoftDeleteAsync(tenantId, "actor-1");

        Assert.True(success);
        var membership = await db.UserTenantMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == "u1" && m.TenantId == tenantId);
        Assert.False(membership.IsActive);
        var user = await db.Users.SingleAsync(u => u.Id == "u1");
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeactivatedAt);
        Assert.Equal("actor-1", user.DeactivatedBy);
        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_SOFT_DELETED,
                "Tenant",
                "actor-1",
                Roles.SuperAdmin,
                It.Is<string?>(d => d != null && d.Contains("Cafe Off")),
                It.Is<string?>(n => n != null && n.Contains("cafe-off")),
                AuditLogStatus.Success,
                null,
                It.IsAny<object?>(),
                null,
                null,
                null),
            Times.Once);
    }

    [Fact]
    public async Task ListAsync_IncludeDeleted_ShowsSoftDeletedTenant()
    {
        await using var db = CreateDb();
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = activeId,
            Name = "Active",
            Slug = "active-one",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = deletedId,
            Name = "Removed",
            Slug = "removed-one",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var defaultList = await service.ListAsync(includeDeleted: false);
        var withDeleted = await service.ListAsync(includeDeleted: true);

        Assert.DoesNotContain(defaultList, t => t.Id == deletedId);
        Assert.Contains(withDeleted, t => t.Id == deletedId);
        Assert.Contains(withDeleted, t => t.Id == activeId);
    }

    [Fact]
    public async Task RestoreAsync_DeletedTenant_SetsActive()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Back",
            Slug = "back-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow,
            DeletedByUserId = "actor-1",
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tenantId,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_RESTORED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.TENANT_RESTORED });

        var service = CreateService(db, auditLog: audit.Object);
        var (success, error) = await service.RestoreAsync(tenantId, "actor-2");

        Assert.True(success);
        Assert.Null(error);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatuses.Active, tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.DeletedAtUtc);
        var membership = await db.UserTenantMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == "u1");
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task HardDeleteAsync_RequiresSoftDeleted_And_EmptyFiscalState()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Empty",
            Slug = "empty-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "empty-tenant" },
            "actor-1");

        Assert.False(result.Success);
        Assert.Contains("soft-deleted", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TenantPermanentDeleteFailureCodes.NotSoftDeleted, result.Code);
    }

    [Fact]
    public async Task HardDeleteAsync_TenantWithRegisters_ThrowsError()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Busy",
            Slug = "busy-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "busy-tenant" },
            "actor-1");

        Assert.False(result.Success);
        Assert.Contains("cash register", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TenantPermanentDeleteFailureCodes.CashRegistersPresent, result.Code);
        Assert.NotNull(result.Dependencies);
        Assert.Equal(1, result.Dependencies!.Dependencies.CashRegisters);
    }

    [Fact]
    public async Task HardDeleteAsync_TenantWithPayments_ThrowsError()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fiscal",
            Slug = "fiscal-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Walk-in",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "sig-test",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "AT-TEST-20260101-001",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "fiscal-tenant" },
            "actor-1");

        Assert.False(result.Success);
        Assert.Equal(TenantPermanentDeleteFailureCodes.FiscalFootprintPresent, result.Code);
        Assert.NotNull(result.Dependencies);
        Assert.True(result.Dependencies!.Dependencies.Payments > 0);
        Assert.True(result.Dependencies.HasFiscalFootprint);
    }

    [Fact]
    public async Task HardDeleteAsync_DeletedTenantWithNoData_RemovesRow()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Gone Forever",
            Slug = "gone-forever",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = "Gone Forever",
            CompanyAddress = "Test 1",
            CompanyTaxNumber = "ATU12345678",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_HARD_DELETED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.TENANT_HARD_DELETED });

        var service = CreateService(db, auditLog: audit.Object);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "gone-forever" },
            "actor-1");

        Assert.True(result.Success);
        Assert.Null(result.Message);
        Assert.False(await db.Tenants.AnyAsync(t => t.Id == tenantId));
        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.TENANT_HARD_DELETED,
                "Tenant",
                "actor-1",
                Roles.SuperAdmin,
                It.Is<string?>(d => d != null && d.Contains("Gone Forever")),
                It.Is<string?>(n => n != null && n.Contains("gone-forever")),
                AuditLogStatus.Success,
                null,
                It.IsAny<object?>(),
                null,
                null,
                null),
            Times.Once);
    }

    [Fact]
    public async Task HardDeleteAsync_RemovesTenantMemberships()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Membership Cleanup",
            Slug = "membership-cleanup",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "u-cleanup",
            TenantId = tenantId,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "membership-cleanup" },
            "actor-1");

        Assert.True(result.Success);
        Assert.Null(result.Message);
        Assert.False(await db.Tenants.AnyAsync(t => t.Id == tenantId));
        Assert.False(await db.UserTenantMemberships.AnyAsync(m => m.TenantId == tenantId));
    }

    [Fact]
    public async Task GetDeleteDependenciesAsync_ReturnsCountsAndBlockersForTenantWithRegister()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Busy",
            Slug = "busy-deps",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var dependencies = await service.GetDeleteDependenciesAsync(tenantId);

        Assert.NotNull(dependencies);
        Assert.Equal("busy-deps", dependencies!.TenantSlug);
        Assert.False(dependencies.CanHardDelete);
        Assert.True(dependencies.HasDependencies);
        Assert.Equal(1, dependencies.Dependencies.CashRegisters);
        Assert.Equal(TenantPermanentDeleteFailureCodes.CashRegistersPresent, dependencies.FailureCode);
        Assert.Contains(
            dependencies.BlockingDependencies,
            b => b.Code == TenantPermanentDeleteFailureCodes.CashRegistersPresent);
    }

    [Fact]
    public async Task HardDeleteAsync_ProductionEnvironment_ReturnsProductionDisabled()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Prod Tenant",
            Slug = "prod-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var production = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var service = CreateService(db, environment: production);
        var result = await service.HardDeleteAsync(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "prod-tenant" },
            "actor-1");

        Assert.False(result.Success);
        Assert.Equal(TenantPermanentDeleteFailureCodes.ProductionPolicy, result.Code);
        Assert.NotNull(result.Dependencies);
    }

    [Fact]
    public async Task GetDeleteDependencies_Controller_ReturnsOk()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Deps",
            Slug = "deps-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tenantDeletion = CreateTenantDeletionService(db);
        var controller = CreateController(tenantService: service, tenantDeletionService: tenantDeletion);

        var actionResult = await controller.GetDeleteDependencies(tenantId);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<TenantDeleteDependenciesDto>(ok.Value);
        Assert.Equal("deps-tenant", dto.TenantSlug);
        Assert.False(dto.CanHardDelete);
    }

    [Fact]
    public async Task GetDeleteDependencies_ReturnsDependencySummary()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Summary Tenant",
            Slug = "summary-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var controller = CreateFullController(db);

        var actionResult = await controller.GetDeleteDependencies(tenantId);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<TenantDeleteDependenciesDto>(ok.Value);
        Assert.Equal("summary-tenant", dto.TenantSlug);
        Assert.Equal(1, dto.Dependencies.CashRegisters);
        Assert.False(dto.CanHardDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.CashRegistersPresent, dto.FailureCode);
        Assert.Contains(
            dto.BlockingDependencies,
            b => b.Code == TenantPermanentDeleteFailureCodes.CashRegistersPresent);
    }

    [Fact]
    public async Task DeletePermanent_InProduction_Returns403()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Prod Tenant",
            Slug = "prod-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var production = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var controller = CreateFullController(db, environment: production);

        var actionResult = await controller.HardDelete(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "prod-tenant" });

        var forbidden = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var body = Assert.IsType<TenantPermanentDeleteErrorResponse>(forbidden.Value);
        Assert.Equal(TenantPermanentDeleteFailureCodes.ProductionPolicy, body.Code);
        Assert.NotNull(body.Dependencies);
    }

    [Fact]
    public async Task DeletePermanent_WithFiscalFootprint_Returns400WithCode()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fiscal",
            Slug = "fiscal-tenant",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Walk-in",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "sig-test",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "AT-TEST-20260101-001",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateFullController(db);

        var actionResult = await controller.HardDelete(
            tenantId,
            new HardDeleteAdminTenantRequest { ConfirmSlug = "fiscal-tenant" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<TenantPermanentDeleteErrorResponse>(badRequest.Value);
        Assert.Equal(TenantPermanentDeleteFailureCodes.FiscalFootprintPresent, body.Code);
        Assert.NotNull(body.Dependencies);
        Assert.True(body.Dependencies!.HasFiscalFootprint);
        Assert.True(body.Dependencies.Dependencies.Payments > 0);
    }

    private static PaymentDetails CreatePendingPayment(Guid cashRegisterId)
    {
        var now = DateTime.UtcNow;
        return new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Customer",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            TseSignature = "pending-signature",
            TseTimestamp = now,
            TaxDetails = JsonDocument.Parse("{\"standard\":20}"),
            PaymentItems = JsonDocument.Parse("[]"),
            ReceiptNumber = "AT-TSE-20260526-0001",
            FinanzOnlineStatus = "Pending",
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
        };
    }

    [Fact]
    public async Task UpdateOperationModeAsync_sets_maintenance_window_and_clears_on_active()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Mode Tenant",
            Slug = $"mode-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            OperationMode = TenantOperationModes.Active,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var ends = DateTime.UtcNow.AddHours(3);
        var (detail, error) = await service.UpdateOperationModeAsync(
            tenantId,
            new UpdateTenantOperationModeRequest
            {
                OperationMode = TenantOperationModes.Maintenance,
                MaintenanceMessage = " Scheduled upgrade ",
                MaintenanceEndsAt = ends,
            },
            actorUserId: "super-admin");

        Assert.Null(error);
        Assert.NotNull(detail);
        Assert.Equal(TenantOperationModes.Maintenance, detail!.OperationMode);
        Assert.Equal("Scheduled upgrade", detail.MaintenanceMessage);
        Assert.NotNull(detail.MaintenanceStartedAt);
        Assert.Equal(ends, detail.MaintenanceEndsAt);

        var (activeDetail, activeError) = await service.UpdateOperationModeAsync(
            tenantId,
            new UpdateTenantOperationModeRequest { OperationMode = TenantOperationModes.Active },
            actorUserId: "super-admin");

        Assert.Null(activeError);
        Assert.Equal(TenantOperationModes.Active, activeDetail!.OperationMode);
        Assert.Null(activeDetail.MaintenanceMessage);
        Assert.Null(activeDetail.MaintenanceStartedAt);
        Assert.Null(activeDetail.MaintenanceEndsAt);
    }

    [Fact]
    public async Task UpdateOperationModeAsync_rejects_invalid_mode_and_deleted_tenant()
    {
        await using var db = CreateDb();
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = activeId,
            Name = "Active Mode Tenant",
            Slug = $"act-{activeId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = deletedId,
            Name = "Deleted Mode Tenant",
            Slug = $"del-{deletedId:N}"[..20],
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (missingMode, invalidError) = await service.UpdateOperationModeAsync(
            activeId,
            new UpdateTenantOperationModeRequest { OperationMode = "offline" },
            actorUserId: "super-admin");
        Assert.Null(missingMode);
        Assert.Contains("Invalid operation mode", invalidError);

        var (deletedResult, deletedError) = await service.UpdateOperationModeAsync(
            deletedId,
            new UpdateTenantOperationModeRequest { OperationMode = TenantOperationModes.Readonly },
            actorUserId: "super-admin");
        Assert.Null(deletedResult);
        Assert.Contains("Deleted tenants", deletedError);
    }

    [Fact]
    public async Task UpdateOperationModeAsync_preserves_started_at_on_subsequent_maintenance_save()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var started = DateTime.UtcNow.AddHours(-2);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Preserve Start",
            Slug = $"prs-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            OperationMode = TenantOperationModes.Maintenance,
            MaintenanceMessage = "old",
            MaintenanceStartedAt = started,
            MaintenanceEndsAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (detail, error) = await service.UpdateOperationModeAsync(
            tenantId,
            new UpdateTenantOperationModeRequest
            {
                OperationMode = TenantOperationModes.Maintenance,
                MaintenanceMessage = "updated",
                MaintenanceEndsAt = DateTime.UtcNow.AddHours(4),
            },
            actorUserId: "super-admin");

        Assert.Null(error);
        Assert.Equal("updated", detail!.MaintenanceMessage);
        Assert.Equal(started, detail.MaintenanceStartedAt);
    }
}
