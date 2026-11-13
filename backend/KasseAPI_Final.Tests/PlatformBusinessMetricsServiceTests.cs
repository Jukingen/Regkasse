using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PlatformBusinessMetricsServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TenantC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Snapshot_CountsWindowsSessionsAndFailedSubmissions_AcrossTenants()
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(TenantA);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, accessor);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Tenants.AddRange(
                Tenant(SystemTenantIds.Platform, "platform"),
                Tenant(TenantA, "alpha"),
                Tenant(TenantB, "beta"),
                Tenant(TenantC, "gamma"));

            db.Receipts.AddRange(
                Receipt(TenantA, Now.AddHours(-2), 10m),
                Receipt(TenantA, Now.AddDays(-3), 20m),
                Receipt(TenantB, Now.AddDays(-10), 30m),
                Receipt(TenantC, Now.AddDays(-40), 99m),
                Receipt(SystemTenantIds.Platform, Now.AddHours(-1), 1000m));

            db.AuthSessions.AddRange(
                Session("pos", revoked: false),
                Session("pos", revoked: true),
                Session("admin", revoked: false));

            db.FinanzOnlineOutboxMessages.AddRange(
                Outbox(FinanzOnlineOutboxStatuses.RetryableFailure),
                Outbox(FinanzOnlineOutboxStatuses.ProtocolFailure),
                Outbox(FinanzOnlineOutboxStatuses.PermanentFailure),
                Outbox(FinanzOnlineOutboxStatuses.DeadLetter),
                Outbox(FinanzOnlineOutboxStatuses.ManualReviewRequired),
                Outbox(FinanzOnlineOutboxStatuses.ProtocolSuccess));

            await db.SaveChangesAsync();
        }

        var snapshot = await new PlatformBusinessMetricsService(factory, new FixedClock(Now))
            .GetSnapshotAsync();

        Assert.Equal(3, snapshot.TotalTenants);
        Assert.Equal(2, snapshot.ActiveTenantsLast30Days);
        Assert.Equal(1, snapshot.ReceiptsIssued.Last24Hours);
        Assert.Equal(2, snapshot.ReceiptsIssued.Last7Days);
        Assert.Equal(3, snapshot.ReceiptsIssued.Last30Days);
        Assert.Equal(10m, snapshot.RevenueEur.Last24Hours);
        Assert.Equal(30m, snapshot.RevenueEur.Last7Days);
        Assert.Equal(60m, snapshot.RevenueEur.Last30Days);
        Assert.Equal(1, snapshot.ActivePosSessions);
        Assert.Equal(5, snapshot.FailedFinanzOnlineSubmissions);
    }

    private static Tenant Tenant(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        Status = TenantStatuses.Active,
        CreatedAt = Now,
    };

    private static Receipt Receipt(Guid tenantId, DateTime issuedAt, decimal total) => new()
    {
        ReceiptId = Guid.NewGuid(),
        TenantId = tenantId,
        PaymentId = Guid.NewGuid(),
        ReceiptNumber = issuedAt.Ticks.ToString(),
        IssuedAt = issuedAt,
        CashRegisterId = Guid.NewGuid(),
        SubTotal = total,
        TaxTotal = 0m,
        GrandTotal = total,
        CreatedAt = issuedAt,
    };

    private static AuthSession Session(string clientApp, bool revoked) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        ClientApp = clientApp,
        CreatedAtUtc = Now,
        RevokedAtUtc = revoked ? Now : null,
    };

    private static FinanzOnlineOutboxMessage Outbox(string status) => new()
    {
        Id = Guid.NewGuid(),
        AggregateType = "Receipt",
        AggregateId = Guid.NewGuid(),
        MessageType = "submit",
        BusinessKey = Guid.NewGuid().ToString("N"),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        PayloadJson = "{}",
        PayloadHash = "hash",
        Mode = "TEST",
        Status = status,
        NextAttemptAt = Now,
    };

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
