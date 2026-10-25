using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineOutboxTenantAccessTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task SuperAdmin_RetryOwnRow_Returns200()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.Pending);
        var service = CreateService(db, signal, Audit(), TenantA);

        var outcome = await service.RetryAsync(row.Id, SuperAdmin(), CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outcome.Row!.Status);
        Assert.Equal(1, signal.SignalCount);
    }

    [Fact]
    public async Task SuperAdmin_RetryForeignRow_Returns200()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantB, FinanzOnlineOutboxStatuses.Pending);
        var service = CreateService(db, signal, Audit(), TenantA);

        var outcome = await service.RetryAsync(row.Id, SuperAdmin(), CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outcome.Row!.Status);
        Assert.Equal(row.Id, signal.LastOutboxId);
        Assert.Equal(1, signal.SignalCount);
    }

    [Fact]
    public async Task Manager_RetryOwnRow_Returns200()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.RetryableFailure);
        var service = CreateService(db, signal, Audit(), TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outcome.Row!.Status);
        Assert.Equal(1, signal.SignalCount);
    }

    [Fact]
    public async Task Manager_RetryForeignRow_Returns404()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantB, FinanzOnlineOutboxStatuses.Pending);
        var service = CreateService(db, signal, Audit(), TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("FINANZONLINE_OUTBOX_NOT_FOUND", outcome.Code);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, (await db.FinanzOnlineOutboxMessages.SingleAsync()).Status);
        Assert.Equal(0, signal.SignalCount);
    }

    [Fact]
    public async Task Manager_MissingAmbientTenant_Returns404()
    {
        await using var db = CreateContext(ambient: null);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.Pending);
        var service = CreateService(db, signal, Audit(), ambient: null);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("FINANZONLINE_OUTBOX_NOT_FOUND", outcome.Code);
        Assert.Equal(0, signal.SignalCount);
    }

    private static FinanzOnlineOutboxActionService CreateService(
        AppDbContext db,
        IFinanzOnlineOutboxWorkerSignal signal,
        IAuditLogService audit,
        Guid? ambient) =>
        new(db, TenantTestDoubles.TenantAccessorReturning(ambient), audit, signal);

    private static AppDbContext CreateContext(Guid? ambient)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FonOutboxAccess_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(ambient));
    }

    private static async Task<FinanzOnlineOutboxMessage> SeedAsync(AppDbContext db, Guid tenantId, string status)
    {
        var row = new FinanzOnlineOutboxMessage
        {
            TenantId = tenantId.ToString("D"),
            AggregateType = "Receipt",
            AggregateId = Guid.NewGuid(),
            MessageType = "SessionReceipt",
            BusinessKey = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PayloadJson = "{}",
            PayloadHash = "hash",
            Status = status,
            AttemptCount = 2,
            CorrelationId = "corr-" + Guid.NewGuid().ToString("N")[..8],
        };
        db.FinanzOnlineOutboxMessages.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static IAuditLogService Audit()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit.Object;
    }

    private static ClaimsPrincipal Manager() => Principal(Roles.Manager, "manager-1");

    private static ClaimsPrincipal SuperAdmin() => Principal(Roles.SuperAdmin, "super-1");

    private static ClaimsPrincipal Principal(string role, string userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
            },
            "Test");
        return new ClaimsPrincipal(identity);
    }
}
