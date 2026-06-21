using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Email;
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
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
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
        return new TenantOnboardingService(
            db,
            provisioningMock,
            Mock.Of<IWelcomeEmailService>(),
            Mock.Of<IAuditLogService>(),
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
        ITenantDeletionService? tenantDeletionService = null,
        IHostEnvironment? environment = null,
        ClaimsPrincipal? user = null)
    {
        var controller = new AdminTenantsController(
            tenantService ?? Mock.Of<IAdminTenantService>(),
            tenantDeletionService ?? Mock.Of<ITenantDeletionService>(),
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
        return CreateController(service, tenantDeletion, environment);
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, string? _, string? __, bool grantTrial, bool _, CancellationToken _) =>
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
            Name = "Cafe",
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
            new CreateAdminTenantRequest { Name = "Test Cafe", Slug = "test-cafe" },
            "actor-1");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("test-cafe", result!.Slug);
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
        Assert.Equal(TenantStatuses.Deleted, row.Status);
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
        Assert.Equal(TenantStatuses.Deleted, tenantRow.Status);
        Assert.False(tenantRow.IsActive);
    }

    [Fact]
    public async Task ListAsync_Includes_Owner_Admin_And_Demo_Preset_Flags()
    {
        await using var db = CreateDb();
        var barId = DemoTenantIds.Bar;
        var cafeId = DemoTenantIds.Cafe;
        db.Tenants.Add(new Tenant
        {
            Id = barId,
            Name = "Test Bar",
            Slug = "bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = cafeId,
            Name = "Café Beispiel",
            Slug = "cafe",
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
            UserId = "owner-bar",
            TenantId = barId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-bar",
            UserName = "admin@bar.regkasse.at",
            Email = "admin@bar.regkasse.at",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListAsync(false);

        var bar = list.Single(x => x.Slug == "bar");
        Assert.Equal("admin@bar.regkasse.at", bar.OwnerAdminEmail);
        Assert.False(bar.IsDemoPreset);

        var cafe = list.Single(x => x.Slug == "cafe");
        Assert.Null(cafe.OwnerAdminEmail);
        Assert.False(cafe.IsDemoPreset);

        var dev = list.Single(x => x.Slug == "dev");
        Assert.True(dev.IsDemoPreset);
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
            Slug = "bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = cafeId,
            Name = "Test Cafe",
            Slug = "cafe",
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
                UserName = "admin@bar.regkasse.at",
                Email = "admin@bar.regkasse.at",
                FirstName = "Bar",
                LastName = "Owner",
                Role = Roles.Manager,
                IsActive = true,
                EmailConfirmed = true,
            },
            new ApplicationUser
            {
                Id = "owner-cafe",
                UserName = "admin@cafe.regkasse.at",
                Email = "admin@cafe.regkasse.at",
                FirstName = "Cafe",
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

        Assert.Equal("admin@bar.regkasse.at", list.Single(x => x.Slug == "bar").OwnerAdminEmail);
        Assert.Equal("admin@cafe.regkasse.at", list.Single(x => x.Slug == "cafe").OwnerAdminEmail);
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
            Name = "Cafe",
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
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-1",
            UserName = "admin@cafe.regkasse.at",
            Email = "admin@cafe.regkasse.at",
            FirstName = "Cafe",
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
        Assert.Equal("admin@cafe.regkasse.at", detail!.OwnerAdminEmail);
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
            FirstName = "Cafe",
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
        var membership = await db.UserTenantMemberships.SingleAsync(m => m.UserId == "u1");
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
        var controller = CreateController(service, tenantDeletion);

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
}
