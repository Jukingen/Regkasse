using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterManagementServiceTests
{
    private static readonly Guid PrimaryTenantId = LegacyDefaultTenantIds.Primary;
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegMgmt_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Tenants.AddRange(
            new Tenant { Id = PrimaryTenantId, Name = "Primary", Slug = "primary" },
            new Tenant { Id = OtherTenantId, Name = "Other", Slug = "other" });
        ctx.SaveChanges();
        return ctx;
    }

    private static CashRegisterManagementService CreateService(
        AppDbContext ctx,
        ISettingsTenantResolver tenantResolver,
        Mock<IAuditLogService>? audit = null)
    {
        audit ??= new Mock<IAuditLogService>();
        audit.Setup(a => a.LogEntityChangeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var enrichment = new Mock<ICashRegisterListEnrichmentService>();
        enrichment
            .Setup(e => e.ApplyAsync(
                It.IsAny<IReadOnlyList<CashRegisterDto>>(),
                It.IsAny<IReadOnlyList<CashRegister>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new CashRegisterManagementService(
            ctx,
            tenantResolver,
            audit.Object,
            enrichment.Object,
            NullLogger<CashRegisterManagementService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_PersistsClosedRegister_WithAudit()
    {
        await using var ctx = CreateContext();
        var audit = new Mock<IAuditLogService>();
        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId), audit);

        var register = await svc.CreateAsync(
            new CreateCashRegisterRequest { RegisterNumber = "KASSE-001", Location = "Hauptkasse" },
            "actor-1",
            "Manager",
            actorIsSuperAdmin: false);

        Assert.Equal(RegisterStatus.Closed, register.Status);
        Assert.Equal("KASSE-001", register.RegisterNumber);
        Assert.Equal(PrimaryTenantId, register.TenantId);
        Assert.Equal("actor-1", register.CreatedBy);

        audit.Verify(
            a => a.LogEntityChangeAsync(
                AuditLogActions.CASH_REGISTER_CREATED,
                AuditLogEntityTypes.CASH_REGISTER,
                register.Id,
                "actor-1",
                "Manager",
                null,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                null),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateRegisterNumber_Throws()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = PrimaryTenantId,
            RegisterNumber = "KASSE-001",
            Location = "X",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(
                new CreateCashRegisterRequest { RegisterNumber = "KASSE-001", Location = "Y" },
                "actor-1",
                "Manager",
                actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task CreateAsync_SuperAdminWithTenantId_TargetsOtherTenant()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        var register = await svc.CreateAsync(
            new CreateCashRegisterRequest
            {
                RegisterNumber = "KASSE-002",
                Location = "Theke",
                TenantId = OtherTenantId,
            },
            "super-1",
            Roles.SuperAdmin,
            actorIsSuperAdmin: true);

        Assert.Equal(OtherTenantId, register.TenantId);
    }

    [Fact]
    public async Task ListAsync_Manager_SeesOnlyEffectiveTenant()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = PrimaryTenantId,
                RegisterNumber = "P-1",
                Location = "Primary",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                TenantId = OtherTenantId,
                RegisterNumber = "O-1",
                Location = "Other",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var page = await svc.ListAsync(null, excludeStatus: null, actorIsSuperAdmin: false, page: 1, pageSize: 20);

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("P-1", page.Items.First().RegisterNumber);
    }

    [Fact]
    public async Task ListAsync_SuperAdminWithTenantId_SeesTargetTenant()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = PrimaryTenantId,
                RegisterNumber = "P-1",
                Location = "Primary",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                TenantId = OtherTenantId,
                RegisterNumber = "O-1",
                Location = "Other",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var page = await svc.ListAsync(OtherTenantId, excludeStatus: null, actorIsSuperAdmin: true, page: 1, pageSize: 20);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("O-1", page.Items.First().RegisterNumber);
    }

    [Fact]
    public async Task ListAsync_ManagerWithForeignTenantFilter_Throws()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ListAsync(OtherTenantId, excludeStatus: null, actorIsSuperAdmin: false, page: 1, pageSize: 20));
    }

    [Fact]
    public async Task ListAsync_SuperAdminWithoutTenantFilter_SeesAllTenants()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = PrimaryTenantId,
                RegisterNumber = "P-1",
                Location = "Primary",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                TenantId = OtherTenantId,
                RegisterNumber = "O-1",
                Location = "Other",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var page = await svc.ListAsync(null, excludeStatus: null, actorIsSuperAdmin: true, page: 1, pageSize: 20);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count());
    }

    [Fact]
    public async Task GetByIdAsync_Manager_CannotReadOtherTenantRegister()
    {
        await using var ctx = CreateContext();
        var otherRegister = new CashRegister
        {
            TenantId = OtherTenantId,
            RegisterNumber = "O-1",
            Location = "Other",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        };
        ctx.CashRegisters.Add(otherRegister);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var dto = await svc.GetByIdAsync(otherRegister.Id, null, actorIsSuperAdmin: false);

        Assert.Null(dto);
    }

    [Fact]
    public async Task GetActiveCountForTenantAsync_ExcludesDecommissioned()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = PrimaryTenantId,
                RegisterNumber = "P-1",
                Location = "A",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                TenantId = PrimaryTenantId,
                RegisterNumber = "P-2",
                Location = "B",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Decommissioned,
            });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var count = await svc.GetActiveCountForTenantAsync(PrimaryTenantId, actorIsSuperAdmin: false);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetActiveCountForTenantAsync_ManagerForeignTenant_Throws()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetActiveCountForTenantAsync(OtherTenantId, actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task GetActiveCountForTenantAsync_SuperAdmin_CanCountOtherTenant()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = OtherTenantId,
            RegisterNumber = "O-1",
            Location = "Other",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var count = await svc.GetActiveCountForTenantAsync(OtherTenantId, actorIsSuperAdmin: true);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetByIdAsync_SuperAdmin_ReturnsRegisterAcrossTenants()
    {
        await using var ctx = CreateContext();
        var otherRegister = new CashRegister
        {
            TenantId = OtherTenantId,
            RegisterNumber = "O-1",
            Location = "Other",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        };
        ctx.CashRegisters.Add(otherRegister);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var dto = await svc.GetByIdAsync(otherRegister.Id, null, actorIsSuperAdmin: true);

        Assert.NotNull(dto);
        Assert.Equal("O-1", dto!.RegisterNumber);
    }

    [Fact]
    public async Task UpdateAsync_Manager_CannotUpdateOtherTenantRegister()
    {
        await using var ctx = CreateContext();
        var otherRegister = new CashRegister
        {
            TenantId = OtherTenantId,
            RegisterNumber = "O-1",
            Location = "Other",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        };
        ctx.CashRegisters.Add(otherRegister);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(
                otherRegister.Id,
                new UpdateCashRegisterRequest { RegisterNumber = "O-2", Location = "X" },
                "manager-1",
                "Manager",
                actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task UpdateAsync_SuperAdmin_CanUpdateOtherTenantRegister()
    {
        await using var ctx = CreateContext();
        var otherRegister = new CashRegister
        {
            TenantId = OtherTenantId,
            RegisterNumber = "O-1",
            Location = "Other",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        };
        ctx.CashRegisters.Add(otherRegister);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));
        var dto = await svc.UpdateAsync(
            otherRegister.Id,
            new UpdateCashRegisterRequest { RegisterNumber = "O-2", Location = "Updated" },
            "super-1",
            Roles.SuperAdmin,
            actorIsSuperAdmin: true);

        Assert.Equal("O-2", dto.RegisterNumber);
        Assert.Equal("Updated", dto.Location);
    }
}
