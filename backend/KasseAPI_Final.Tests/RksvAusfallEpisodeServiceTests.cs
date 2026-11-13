using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvAusfallEpisodeServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Ausfall_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static RksvAusfallEpisodeService CreateService(
        AppDbContext db,
        string tseMode = "Device",
        bool autoEnqueue = false,
        IFinanzOnlineOutboxService? outbox = null)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new RksvAusfallEpisodeService(
            db,
            outbox ?? new FinanzOnlineOutboxService(db, NullLogger<FinanzOnlineOutboxService>.Instance),
            Options.Create(new AusfallOptions { AutoEnqueue = autoEnqueue, AusfallGraceMinutes = 30 }).ToMonitor(),
            Options.Create(new FinanzOnlineModeOptions { Mode = "Test" }).ToMonitor(),
            Options.Create(new FinanzOnlineCutoverGuardOptions()).ToMonitor(),
            Options.Create(new TseOptions { TseMode = tseMode }).ToMonitor(),
            audit.Object,
            activity.Object,
            CreateFeatureFlags(autoEnqueue),
            NullLogger<RksvAusfallEpisodeService>.Instance);
    }

    private static IFeatureFlagService CreateFeatureFlags(bool autoAusfallEnabled)
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string name, string? _) =>
                autoAusfallEnabled
                && string.Equals(
                    FeatureFlagNames.Normalize(name),
                    FeatureFlagNames.EnableAutoAusfall,
                    StringComparison.Ordinal));
        return flags.Object;
    }

    [Fact]
    public async Task Trigger_DemoMode_SkipsFon()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db, tseMode: "Demo");
        var result = await svc.TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                EpisodeType = "SCU",
                CertificateSerial = "CERT",
                Begruendung = RksvAusfallBegruendungCodes.Other,
            },
            tenantId,
            "user1",
            "Manager");
        Assert.False(result.Success);
        Assert.Equal("AUSFALL_DEMO_SOFT_SKIP", result.ErrorCode);
        Assert.Empty(db.RksvAusfallEpisodes);
    }

    [Fact]
    public async Task Trigger_CreatesSuggestedEpisode_WithoutAutoEnqueue()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);
        var result = await svc.TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                EpisodeType = "SCU",
                CertificateSerial = "CERT-ABC",
                Begruendung = RksvAusfallBegruendungCodes.HardwareDefect,
                BeginnUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
            tenantId,
            "user1",
            "Manager");
        Assert.True(result.Success);
        Assert.NotNull(result.Episode);
        Assert.Equal(RksvAusfallEpisodeStatuses.Suggested, result.Episode!.Status);
        Assert.Null(result.Episode.OutboxMessageId);
        Assert.Equal(1, await db.RksvAusfallEpisodes.CountAsync());
        Assert.Equal(0, await db.FinanzOnlineOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Approve_EnqueuesOutbox()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);
        var created = await svc.TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                EpisodeType = "SCU",
                CertificateSerial = "CERT-APPR",
                Begruendung = RksvAusfallBegruendungCodes.SoftwareDefect,
                BeginnUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            tenantId,
            "user1",
            "Manager");
        Assert.True(created.Success);

        var approved = await svc.ApproveAndEnqueueAsync(
            created.Episode!.Id,
            tenantId,
            "user1",
            "Manager",
            "approved");
        Assert.True(approved.Success);
        Assert.Equal(RksvAusfallEpisodeStatuses.Submitted, approved.Episode!.Status);
        Assert.NotNull(approved.Episode.OutboxMessageId);
        Assert.Equal(1, await db.FinanzOnlineOutboxMessages.CountAsync());
        var msg = await db.FinanzOnlineOutboxMessages.SingleAsync();
        Assert.Equal(FinanzOnlineRksvAusfallOutboxMessageTypes.RksvAusfallSeSubmission, msg.MessageType);
    }

    [Fact]
    public async Task SuggestAusfallFromFailover_CreatesSuggestion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var device = new TseDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = "SE-FAILOVER",
            DeviceType = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsPrimary = true,
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SuggestAusfallFromFailoverAsync(device);
        var episode = await db.RksvAusfallEpisodes.SingleAsync();
        Assert.Equal(RksvAusfallEpisodeStatuses.Suggested, episode.Status);
        Assert.Equal(RksvAusfallOperationKinds.Ausfall, episode.OperationKind);
        Assert.Equal(device.Id, episode.DeviceId);
    }

    [Fact]
    public async Task MarkManualPortal_ClosesEpisode()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var svc = CreateService(db);
        var created = await svc.TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                EpisodeType = "SCU",
                CertificateSerial = "CERT-M",
                Begruendung = RksvAusfallBegruendungCodes.Other,
                BeginnUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            },
            tenantId,
            "u",
            "Manager");
        var marked = await svc.MarkManualPortalAsync(
            created.Episode!.Id,
            tenantId,
            "u",
            "Manager",
            new RksvAusfallMarkManualRequest { ExternalReference = "PORTAL-1", OperatorNote = "done in FON UI" });
        Assert.True(marked.Success);
        Assert.Equal(RksvAusfallEpisodeStatuses.Closed, marked.Episode!.Status);
        Assert.Equal("PORTAL-1", marked.Episode.ExternalReference);
    }
}
