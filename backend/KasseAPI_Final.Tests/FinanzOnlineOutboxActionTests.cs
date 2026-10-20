using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineOutboxActionTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Retry_FailedRow_ResetsToPending_AndSignalsWorker()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var audit = new Mock<IAuditLogService>();
        SetupAudit(audit);
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.RetryableFailure, attemptCount: 4);
        var service = CreateService(db, signal, audit.Object, TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outcome.Row!.Status);
        Assert.Equal(0, outcome.Row.AttemptCount);
        Assert.Null(outcome.Row.ProcessedAt);
        Assert.Null(outcome.Row.LastErrorCode);
        Assert.Equal(1, signal.SignalCount);
        Assert.Equal(row.Id, signal.LastOutboxId);
        VerifyAudit(audit, AuditEventType.FinanzOnlineOutboxRetried);
    }

    [Fact]
    public async Task Retry_ProtocolSuccess_Returns400_AndDoesNotSignal()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.ProtocolSuccess, attemptCount: 1);
        var service = CreateService(db, signal, new Mock<IAuditLogService>().Object, TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("FINANZONLINE_OUTBOX_ALREADY_SENT", outcome.Code);
        Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, (await db.FinanzOnlineOutboxMessages.SingleAsync()).Status);
        Assert.Equal(0, signal.SignalCount);
    }

    [Fact]
    public async Task Retry_AwaitingProtocol_Returns400()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.AwaitingProtocol, attemptCount: 1);
        var service = CreateService(db, signal, new Mock<IAuditLogService>().Object, TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal(0, signal.SignalCount);
    }

    [Fact]
    public async Task Retry_Processing_Returns409_AndDoesNotSignal()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.Processing, attemptCount: 1);
        var service = CreateService(db, signal, new Mock<IAuditLogService>().Object, TenantA);

        var outcome = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Equal("FINANZONLINE_OUTBOX_IN_PROGRESS", outcome.Code);
        Assert.Equal(0, signal.SignalCount);
    }

    [Fact]
    public async Task RetryFailed_ResetsOnlyMatchingFailedRows_AndSignalsOnce()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var audit = new Mock<IAuditLogService>();
        SetupAudit(audit);
        var register = Guid.NewGuid();
        await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.DeadLetter, registerId: register);
        await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.PermanentFailure, registerId: register);
        await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.ProtocolSuccess, registerId: register);
        await SeedAsync(db, TenantB, FinanzOnlineOutboxStatuses.RetryableFailure, registerId: register);
        await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.ProtocolFailure, registerId: Guid.NewGuid());
        var service = CreateService(db, signal, audit.Object, TenantA);

        var outcome = await service.RetryFailedAsync(
            new FinanzOnlineOutboxRetryFailedRequest { CashRegisterId = register.ToString() },
            Manager(),
            CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(2, outcome.RetriedCount);
        var rows = await db.FinanzOnlineOutboxMessages.ToListAsync();
        Assert.Equal(2, rows.Count(x => x.Status == FinanzOnlineOutboxStatuses.Pending && x.AttemptCount == 0));
        Assert.Contains(rows, x => x.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess);
        Assert.Contains(rows, x => x.TenantId == TenantB.ToString("D") && x.Status == FinanzOnlineOutboxStatuses.RetryableFailure);
        Assert.Contains(rows, x => x.Status == FinanzOnlineOutboxStatuses.ProtocolFailure);
        Assert.Equal(1, signal.SignalCount);
        Assert.Null(signal.LastOutboxId);
        VerifyAudit(audit, AuditEventType.FinanzOnlineOutboxBulkRetried);
    }

    [Fact]
    public async Task MarkResolved_StoresReasonWithoutSignaling()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var audit = new Mock<IAuditLogService>();
        SetupAudit(audit);
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.PermanentFailure);
        var service = CreateService(db, signal, audit.Object, TenantA);

        var outcome = await service.MarkResolvedAsync(
            row.Id,
            new FinanzOnlineOutboxMarkResolvedRequest { Reason = "  Operator closed after phone confirmation  " },
            SuperAdmin(),
            CancellationToken.None);

        Assert.Equal(200, outcome.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.Resolved, outcome.Row!.Status);
        Assert.Equal("Operator closed after phone confirmation", outcome.Row.ResolutionReason);
        Assert.Equal("super-1", outcome.Row.ResolvedByUserId);
        Assert.NotNull(outcome.Row.ResolvedAtUtc);
        Assert.Equal(0, signal.SignalCount);
        VerifyAudit(audit, AuditEventType.FinanzOnlineOutboxMarkedResolved);
    }

    [Fact]
    public async Task MarkResolved_MissingReason_Returns400()
    {
        await using var db = CreateContext(TenantA);
        var row = await SeedAsync(db, TenantA, FinanzOnlineOutboxStatuses.PermanentFailure);
        var service = CreateService(db, new FinanzOnlineOutboxWorkerSignal(), new Mock<IAuditLogService>().Object, TenantA);

        var outcome = await service.MarkResolvedAsync(row.Id, new FinanzOnlineOutboxMarkResolvedRequest { Reason = "  " }, SuperAdmin(), CancellationToken.None);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("FINANZONLINE_OUTBOX_REASON_REQUIRED", outcome.Code);
        Assert.Equal(FinanzOnlineOutboxStatuses.PermanentFailure, (await db.FinanzOnlineOutboxMessages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Retry_CrossTenant_Returns404()
    {
        await using var db = CreateContext(TenantA);
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var row = await SeedAsync(db, TenantB, FinanzOnlineOutboxStatuses.DeadLetter);
        var service = CreateService(db, signal, new Mock<IAuditLogService>().Object, TenantA);

        var asManager = await service.RetryAsync(row.Id, Manager(), CancellationToken.None);

        Assert.Equal(404, asManager.StatusCode);
        Assert.Equal(FinanzOnlineOutboxStatuses.DeadLetter, (await db.FinanzOnlineOutboxMessages.SingleAsync()).Status);
        Assert.Equal(0, signal.SignalCount);
    }

    [Fact]
    public async Task MarkResolved_RequiresSystemCritical_ManagerDenied_SuperAdminAllowed()
    {
        var method = typeof(FinanzOnlineOutboxAdminController).GetMethod(nameof(FinanzOnlineOutboxAdminController.MarkResolved));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true).Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.SystemCritical, attribute.Permission);

        var retry = typeof(FinanzOnlineOutboxAdminController).GetMethod(nameof(FinanzOnlineOutboxAdminController.Retry));
        var retryAttribute = Assert.Single(retry!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true).Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.FinanzOnlineSubmit, retryAttribute.Permission);

        var auth = BuildAuthorizationServices().GetRequiredService<IAuthorizationService>();
        var manager = await auth.AuthorizeAsync(Manager(), null, attribute.Policy!);
        var super = await auth.AuthorizeAsync(SuperAdmin(), null, attribute.Policy!);
        var managerSubmit = await auth.AuthorizeAsync(Manager(), null, retryAttribute.Policy!);

        Assert.False(manager.Succeeded);
        Assert.True(super.Succeeded);
        Assert.True(managerSubmit.Succeeded);
    }

    [Fact]
    public async Task WorkerSignal_InterruptsPollDelay()
    {
        var signal = new FinanzOnlineOutboxWorkerSignal();
        var pending = signal.DelayAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        var id = Guid.NewGuid();
        signal.Signal(id);

        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(id, signal.LastOutboxId);
    }

    private static FinanzOnlineOutboxActionService CreateService(
        AppDbContext db,
        IFinanzOnlineOutboxWorkerSignal signal,
        IAuditLogService audit,
        Guid ambient) =>
        new(db, TenantTestDoubles.TenantAccessorReturning(ambient), audit, signal);

    private static AppDbContext CreateContext(Guid ambient)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FonOutbox_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(ambient));
    }

    private static async Task<FinanzOnlineOutboxMessage> SeedAsync(
        AppDbContext db,
        Guid tenantId,
        string status,
        int attemptCount = 2,
        Guid? registerId = null)
    {
        var row = new FinanzOnlineOutboxMessage
        {
            TenantId = tenantId.ToString("D"),
            AggregateType = "Receipt",
            AggregateId = Guid.NewGuid(),
            MessageType = "SessionReceipt",
            BusinessKey = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PayloadJson = registerId is Guid register
                ? "{\"Scope\":{\"RegisterId\":\"" + register + "\"}}"
                : "{}",
            PayloadHash = "hash",
            Status = status,
            AttemptCount = attemptCount,
            LastErrorCode = "FON-421",
            ProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            CorrelationId = "corr-" + Guid.NewGuid().ToString("N")[..8],
        };
        db.FinanzOnlineOutboxMessages.Add(row);
        await db.SaveChangesAsync();
        return row;
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

    private static void SetupAudit(Mock<IAuditLogService> audit)
    {
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
    }

    private static void VerifyAudit(Mock<IAuditLogService> audit, AuditEventType actionType)
    {
        audit.Verify(a => a.LogSystemOperationAsync(
                actionType.ToString(),
                nameof(FinanzOnlineOutboxMessage),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                actionType,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    private static ServiceProvider BuildAuthorizationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenantAccessor>(NullCurrentTenantAccessor.Instance);
        services.AddAppAuthorization();
        return services.BuildServiceProvider();
    }
}
