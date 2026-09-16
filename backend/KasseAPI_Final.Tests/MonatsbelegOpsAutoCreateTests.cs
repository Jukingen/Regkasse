using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MonatsbelegOpsAutoCreateTests
{
    private sealed class FixedUtcTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    /// <summary>2026-09-01 00:05 Vienna (CEST).</summary>
    private static readonly DateTime Day1After0001Utc = new(2026, 8, 31, 22, 5, 0, DateTimeKind.Utc);

    /// <summary>2026-09-15 Vienna — outside 7-day catch-up.</summary>
    private static readonly DateTime MidMonthUtc = new(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAutoCreateAsync_OutsideWindow_ReturnsZero()
    {
        var harness = await CreateHarnessAsync(MidMonthUtc);
        await using var _ = harness.Context;

        var created = await harness.Sut.RunAutoCreateAsync();

        Assert.Equal(0, created);
        harness.SpecialReceipts.Verify(
            s => s.CreateMonatsbelegAsync(
                It.IsAny<CreateMonatsbelegRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAutoCreateAsync_CreatesPreviousMonth_AndAuditsSystemActor()
    {
        var harness = await CreateHarnessAsync(Day1After0001Utc, hasMonatsbeleg: false);
        await using var _ = harness.Context;

        var created = await harness.Sut.RunAutoCreateAsync();

        Assert.Equal(1, created);
        harness.SpecialReceipts.Verify(
            s => s.CreateMonatsbelegAsync(
                It.Is<CreateMonatsbelegRequest>(r => r.Year == 2026 && r.Month == 8 && r.CashRegisterId == harness.RegisterId),
                AutoMonatsbelegCutoff.SystemActorUserId,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Activity.Verify(
            a => a.PublishAsync(
                It.Is<ActivityEventPublishRequest>(r => r.Type == ActivityEventType.MonatsbelegCreated),
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Audit.Verify(
            a => a.LogSystemOperationAsync(
                "MONATSBELEG_CREATED",
                It.IsAny<string>(),
                AutoMonatsbelegCutoff.SystemActorUserId,
                AutoMonatsbelegCutoff.SystemActorRole,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.MonatsbelegCreated,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()),
            Times.Once);

        var run = await harness.Context.MonatsbelegAutoRuns.AsNoTracking().SingleAsync();
        Assert.Equal(MonatsbelegAutoRunStatuses.Succeeded, run.Status);
    }

    [Fact]
    public async Task RunAutoCreateAsync_ExhaustsRetries_EmitsFailure()
    {
        var harness = await CreateHarnessAsync(Day1After0001Utc, hasMonatsbeleg: false, createThrows: true);
        await using var _ = harness.Context;

        var created = await harness.Sut.RunAutoCreateAsync();

        Assert.Equal(0, created);
        harness.SpecialReceipts.Verify(
            s => s.CreateMonatsbelegAsync(
                It.IsAny<CreateMonatsbelegRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        harness.Activity.Verify(
            a => a.PublishAsync(
                It.Is<ActivityEventPublishRequest>(r => r.Type == ActivityEventType.MonatsbelegAutoCreateFailed),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var run = await harness.Context.MonatsbelegAutoRuns.AsNoTracking().SingleAsync();
        Assert.Equal(MonatsbelegAutoRunStatuses.Exhausted, run.Status);
        Assert.Equal(3, run.AttemptCount);
    }

    private static async Task<Harness> CreateHarnessAsync(
        DateTime utcNow,
        bool hasMonatsbeleg = false,
        bool createThrows = false)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MbAuto_{Guid.NewGuid():N}")
            .Options;
        var ctx = new AppDbContext(options, accessor);
        TenantTestDoubles.EnsurePlatformTenant(ctx);

        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Test GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            AutoMonatsbelegEnabled = true,
            MonatsbelegRetryCount = 3,
        });
        await ctx.SaveChangesAsync();

        var policy = new Mock<IRksvMonatsbelegPolicy>();
        policy.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasMonatsbeleg);

        var special = new Mock<IRksvSpecialReceiptService>();
        if (createThrows)
        {
            special.Setup(s => s.CreateMonatsbelegAsync(
                    It.IsAny<CreateMonatsbelegRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("TSE unavailable"));
        }
        else
        {
            special.Setup(s => s.CreateMonatsbelegAsync(
                    It.IsAny<CreateMonatsbelegRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateMonatsbelegResponse
                {
                    PaymentId = Guid.NewGuid(),
                    ReceiptId = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(),
                    ReceiptNumber = "1",
                    QrData = "header.payload.signature",
                    Year = 2026,
                    Month = 8,
                    CreatedAtUtc = utcNow,
                });
        }

        var activity = new Mock<IActivityEventService>();
        activity.Setup(a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityEventPublishRequest req, CancellationToken _) => new ActivityEvent
            {
                Id = Guid.NewGuid(),
                TenantId = req.TenantId,
                Type = req.Type,
                Title = req.Title,
            });

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());

        var opt = new Mock<IOptionsMonitor<MonatsbelegOpsOptions>>();
        opt.Setup(o => o.CurrentValue).Returns(new MonatsbelegOpsOptions
        {
            AutoCreateEnabled = true,
            ReminderEnabled = true,
            CatchUpThroughDay = 7,
            RetryBackoffEnabled = false,
            CheckIntervalMinutes = 15,
        });

        var sut = new MonatsbelegOpsService(
            ctx,
            TenantTestDoubles.PrimaryTenantResolver,
            accessor,
            policy.Object,
            special.Object,
            activity.Object,
            audit.Object,
            opt.Object,
            new FixedUtcTimeProvider(utcNow),
            NullLogger<MonatsbelegOpsService>.Instance);

        return new Harness(ctx, sut, special, activity, audit, regId);
    }

    private sealed record Harness(
        AppDbContext Context,
        MonatsbelegOpsService Sut,
        Mock<IRksvSpecialReceiptService> SpecialReceipts,
        Mock<IActivityEventService> Activity,
        Mock<IAuditLogService> Audit,
        Guid RegisterId);
}
