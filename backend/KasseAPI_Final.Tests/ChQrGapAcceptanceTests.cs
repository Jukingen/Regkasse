using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ChQrGapAcceptanceTests
{
    [Fact]
    public async Task Accept_ValidGaps_PersistsOverride_AndWritesAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(Tenant(tenantId, "bern"));
        await db.SaveChangesAsync();

        var calls = new List<AuditCall>();
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsAccepted,
                It.IsAny<object?>(),
                "manager1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = Controller(db, tenantId, calls, activity.Object, "manager1");
        var catalog = new ChQrKnownGapCatalog();
        var gaps = catalog.Gaps.Select(gap => gap.Id).ToArray();

        var result = await controller.Accept(
            tenantId,
            new AcceptChQrGapsRequest(gaps),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ChQrGapAcceptanceResponse>(ok.Value);
        Assert.Equal(gaps, body.Acceptance!.AcceptedGaps);
        Assert.Equal("manager1", body.Acceptance.AcceptedBy);
        Assert.Equal(DateTimeKind.Utc, body.Acceptance.AcceptedAtUtc.Kind);

        var row = await db.TenantSettings.SingleAsync();
        Assert.Equal(tenantId, row.TenantId);
        Assert.Equal(ChQrGapAcceptanceService.SettingsKey, row.Key);
        Assert.Contains("manager1", row.Value, StringComparison.Ordinal);
        Assert.Contains(gaps[0], row.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tenantId\":null", row.Value, StringComparison.OrdinalIgnoreCase);

        var call = Assert.Single(calls);
        Assert.Equal(AuditEventType.ChQrKnownGapsAccepted, call.Type);
        Assert.Equal(114, (int)AuditEventType.ChQrKnownGapsAccepted);
        Assert.Equal(tenantId, call.TenantId);
        activity.VerifyAll();
    }

    [Fact]
    public async Task Accept_UnknownGap_Returns400_WithInvalidId()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(Tenant(tenantId, "bern"));
        await db.SaveChangesAsync();

        var controller = Controller(db, tenantId, [], new Mock<IActivityEventPublisher>().Object, "manager1");
        var result = await controller.Accept(
            tenantId,
            new AcceptChQrGapsRequest(["not-a-real-gap"]),
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains("not-a-real-gap", json, StringComparison.Ordinal);
        Assert.Empty(await db.TenantSettings.ToListAsync());
    }

    [Fact]
    public async Task Get_OtherTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        db.Tenants.Add(Tenant(tenantA, "a"));
        db.Tenants.Add(Tenant(tenantB, "b"));
        await db.SaveChangesAsync();

        var controller = Controller(db, tenantA, [], new Mock<IActivityEventPublisher>().Object, "manager1");
        var result = await controller.Get(tenantB, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsEveryFixtureGap()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(Tenant(tenantId, "bern"));
        await db.SaveChangesAsync();

        var controller = Controller(db, tenantId, [], new Mock<IActivityEventPublisher>().Object, "manager1");
        var result = await controller.Get(tenantId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ChQrGapAcceptanceResponse>(ok.Value);

        var fixturePath = Path.Combine(
            FindBackendRoot(),
            "Services",
            "Countries",
            "QrRechnung",
            "ChQrKnownGaps.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var fixtureIds = fixture.RootElement.GetProperty("gaps")
            .EnumerateArray()
            .Select(row => row.GetProperty("id").GetString())
            .ToArray();

        Assert.Equal(fixtureIds, body.KnownGaps.Select(gap => gap.Id).ToArray());
        Assert.Null(body.Acceptance);
    }

    [Fact]
    public async Task Get_IgnoresAGlobalSettingsRow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(Tenant(tenantId, "bern"));
        db.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            Key = ChQrGapAcceptanceService.SettingsKey,
            Value = """{"acceptedGaps":["official-swiss-cross"],"acceptedBy":"global","acceptedAtUtc":"2026-09-30T12:00:00Z"}""",
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = Service(db, new Mock<IActivityEventPublisher>().Object, []);
        var acceptance = await service.GetAsync(tenantId);

        Assert.Null(acceptance);
    }

    [Fact]
    public async Task BuildPayload_UnacceptedOpenGaps_WarnsAndStillReturnsPayload()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsOutstanding,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var builder = new QrRechnungBuilder(
            Flags(true),
            gapAcceptance: Service(db, activity.Object, []));
        var payload = await builder.BuildPayloadAsync(Request(tenantId));

        Assert.StartsWith("SPC", payload.SwissQrText, StringComparison.Ordinal);
        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsOutstanding,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(261, (int)ActivityEventType.ChQrKnownGapsOutstanding);
    }

    [Fact]
    public async Task BuildPayload_WhenOpenGapsAreAccepted_DoesNotWarn()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var catalog = new ChQrKnownGapCatalog();
        var open = catalog.Gaps.Where(gap => !gap.Present).Select(gap => gap.Id).ToArray();
        var calls = new List<AuditCall>();
        var service = Service(db, new Mock<IActivityEventPublisher>().Object, calls);
        await service.AcceptAsync(tenantId, open, "manager1");

        var activity = new Mock<IActivityEventPublisher>();
        var builder = new QrRechnungBuilder(Flags(true), gapAcceptance: Service(db, activity.Object, []));
        var payload = await builder.BuildPayloadAsync(Request(tenantId));

        Assert.StartsWith("SPC", payload.SwissQrText, StringComparison.Ordinal);
        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsOutstanding,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ChQrGapAcceptanceService Service(
        AppDbContext db,
        IActivityEventPublisher activity,
        List<AuditCall> calls) =>
        new(db, new ChQrKnownGapCatalog(), AuditCapturing(calls).Object, NullLogger<ChQrGapAcceptanceService>.Instance, activity);

    private static AdminChQrGapAcceptanceController Controller(
        AppDbContext db,
        Guid ambient,
        List<AuditCall> calls,
        IActivityEventPublisher activity,
        string actorName)
    {
        var controller = new AdminChQrGapAcceptanceController(
            Service(db, activity, calls),
            db,
            TenantTestDoubles.TenantAccessorReturning(ambient));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, actorName),
                    new Claim(ClaimTypes.NameIdentifier, "admin"),
                ],
                "test")),
            },
        };
        return controller;
    }

    private static Mock<IAuditLogService> AuditCapturing(List<AuditCall> calls)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback(new InvocationAction(invocation =>
            {
                calls.Add(new AuditCall(
                    invocation.Arguments[12] as AuditEventType?,
                    invocation.Arguments[14] as Guid?,
                    invocation.Arguments[16]));
            }))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingQrRechnung, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static QrRechnungRequest Request(Guid tenantId) => new(
        Iban: "CH9300762011623852957",
        Creditor: new QrRechnungParty("CH GmbH", "Bahnhofstrasse 1", null, "8001", "Zürich", "CH"),
        Debtor: null,
        Amount: 10m,
        Currency: "CHF",
        Reference: null,
        AdditionalInfo: null,
        ReferenceType: QrRechnungReferenceType.Non,
        InvoiceId: Guid.NewGuid(),
        TenantId: tenantId);

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ChQrGaps_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static Tenant Tenant(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        IsActive = true,
    };

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private sealed record AuditCall(AuditEventType? Type, Guid? TenantId, object? NewValues);
}
