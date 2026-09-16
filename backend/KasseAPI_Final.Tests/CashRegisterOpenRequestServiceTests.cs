using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterOpenRequestServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RegisterId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task CreateAsync_creates_pending_and_publishes_activity()
    {
        var activity = new Mock<IActivityEventPublisher>();
        activity
            .Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.CashRegisterOpenRequested,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (sut, db) = CreateSut(nameof(CreateAsync_creates_pending_and_publishes_activity), activity: activity.Object);
        SeedClosedRegister(db);
        await db.SaveChangesAsync();

        var result = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Request);
        Assert.Equal(CashRegisterOpenRequestStatuses.Pending, result.Request!.Status);
        Assert.Equal(RegisterId, result.Request.CashRegisterId);

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.CashRegisterOpenRequested,
                It.IsAny<object?>(),
                "cashier-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_returns_existing_pending_without_duplicate()
    {
        var (sut, db) = CreateSut(nameof(CreateAsync_returns_existing_pending_without_duplicate));
        SeedClosedRegister(db);
        await db.SaveChangesAsync();

        var first = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        var second = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(CashRegisterOpenRequestService.AlreadyPendingCode, second.Code);
        Assert.Equal(first.Request!.Id, second.Request!.Id);
        Assert.Equal(1, await db.CashRegisterOpenRequests.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_register()
    {
        var (sut, _) = CreateSut(nameof(CreateAsync_rejects_missing_register));
        var result = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = Guid.NewGuid() });
        Assert.False(result.Succeeded);
        Assert.Equal(CashRegisterOpenRequestService.RegisterNotFoundCode, result.Code);
    }

    [Fact]
    public async Task CreateAsync_rejects_assigned_to_other_user()
    {
        var (sut, db) = CreateSut(nameof(CreateAsync_rejects_assigned_to_other_user));
        SeedClosedRegister(db, assignedUserId: "other-cashier");
        await db.SaveChangesAsync();

        var result = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        Assert.False(result.Succeeded);
        Assert.Equal(CashRegisterOpenRequestService.RegisterAssignedToOtherCode, result.Code);
    }

    [Fact]
    public async Task ApproveAsync_opens_register_as_requester_and_marks_approved()
    {
        var shift = new Mock<ICashRegisterShiftService>();
        shift
            .Setup(s => s.TryOpenCashRegisterAsync(
                RegisterId,
                "cashier-1",
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterOpenResult.Opened("K1"));

        var (sut, db) = CreateSut(nameof(ApproveAsync_opens_register_as_requester_and_marks_approved), shift: shift.Object);
        SeedClosedRegister(db);
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        var approved = await sut.ApproveAsync(
            created.Request!.Id,
            "manager-1",
            Roles.Manager,
            actorIsSuperAdmin: false,
            new ResolveCashRegisterOpenRequestBody { Note = "ok" });

        Assert.True(approved.Succeeded);
        Assert.Equal(CashRegisterOpenRequestStatuses.Approved, approved.Request!.Status);
        Assert.Equal("manager-1", approved.Request.ResolvedByUserId);
        Assert.Equal("ok", approved.Request.ResolutionNote);
        shift.Verify(
            s => s.TryOpenCashRegisterAsync(
                RegisterId,
                "cashier-1",
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_does_not_resolve_when_open_fails()
    {
        var shift = new Mock<ICashRegisterShiftService>();
        shift
            .Setup(s => s.TryOpenCashRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterOpenResult.StartbelegRequired());

        var (sut, db) = CreateSut(nameof(ApproveAsync_does_not_resolve_when_open_fails), shift: shift.Object);
        SeedClosedRegister(db);
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        var approved = await sut.ApproveAsync(
            created.Request!.Id,
            "manager-1",
            Roles.Manager,
            actorIsSuperAdmin: false,
            null);

        Assert.False(approved.Succeeded);
        Assert.Equal(CashRegisterOpenRequestService.StartbelegRequiredCode, approved.Code);
        var row = await db.CashRegisterOpenRequests.SingleAsync();
        Assert.Equal(CashRegisterOpenRequestStatuses.Pending, row.Status);
    }

    [Fact]
    public async Task DenyAsync_marks_denied()
    {
        var (sut, db) = CreateSut(nameof(DenyAsync_marks_denied));
        SeedClosedRegister(db);
        await db.SaveChangesAsync();

        var created = await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });
        var denied = await sut.DenyAsync(
            created.Request!.Id,
            "manager-1",
            Roles.Manager,
            actorIsSuperAdmin: false,
            new ResolveCashRegisterOpenRequestBody { Note = "busy" });

        Assert.True(denied.Succeeded);
        Assert.Equal(CashRegisterOpenRequestStatuses.Denied, denied.Request!.Status);
        Assert.Equal("busy", denied.Request.ResolutionNote);
    }

    [Fact]
    public async Task ListAsync_returns_pending_for_tenant()
    {
        var (sut, db) = CreateSut(nameof(ListAsync_returns_pending_for_tenant));
        SeedClosedRegister(db);
        await db.SaveChangesAsync();
        await sut.CreateAsync("cashier-1", new CreateCashRegisterOpenRequestBody { RegisterId = RegisterId });

        var pending = await sut.ListAsync(CashRegisterOpenRequestStatuses.Pending, tenantIdFilter: null, actorIsSuperAdmin: false);
        Assert.Single(pending);
        Assert.Equal("K1", pending[0].RegisterNumber);
    }

    private static void SeedClosedRegister(AppDbContext db, string? assignedUserId = null)
    {
        TenantTestDoubles.EnsureTenant(db, TenantId, "cafe-a");
        db.CashRegisters.Add(new CashRegister
        {
            Id = RegisterId,
            TenantId = TenantId,
            RegisterNumber = "K1",
            Location = "Theke",
            StartingBalance = 100m,
            CurrentBalance = 100m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = assignedUserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static (CashRegisterOpenRequestService Sut, AppDbContext Db) CreateSut(
        string name,
        IActivityEventPublisher? activity = null,
        ICashRegisterShiftService? shift = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(TenantId);
        var db = new AppDbContext(options, accessor);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var sut = new CashRegisterOpenRequestService(
            db,
            accessor,
            shift ?? Mock.Of<ICashRegisterShiftService>(),
            activity ?? Mock.Of<IActivityEventPublisher>(),
            audit.Object,
            TimeProvider.System,
            NullLogger<CashRegisterOpenRequestService>.Instance);
        return (sut, db);
    }
}
