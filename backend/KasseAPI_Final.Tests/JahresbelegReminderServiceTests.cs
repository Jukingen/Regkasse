using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class JahresbelegDeadlineTests
{
    [Theory]
    [InlineData(2026, 12, 1, true)]
    [InlineData(2026, 12, 2, false)]
    [InlineData(2026, 11, 30, false)]
    [InlineData(2027, 1, 31, false)]
    public void IsReminderDay_IsOnlyDecemberFirst(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, JahresbelegDeadline.IsReminderDay(new DateTime(year, month, day)));
    }

    [Fact]
    public void YearDue_InJanuary_IsThePreviousYear()
    {
        Assert.Equal(2026, JahresbelegDeadline.YearDue(new DateTime(2026, 12, 1)));
        Assert.Equal(2026, JahresbelegDeadline.YearDue(new DateTime(2027, 1, 15)));
        Assert.Equal(new DateOnly(2027, 1, 31), JahresbelegDeadline.Deadline(2026));
    }
}

public sealed class JahresbelegReminderServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TenantDe = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTime DecemberFirstUtc = new(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CheckAndNotify_OnDecemberFirst_WarnsAustrianTenantsMissingJahresbeleg()
    {
        var (sut, published, db) = CreateSut(DecemberFirstUtc);
        await using (db)
        {
            SeedTenant(db, TenantA, "Alpha", "AT", hasJahresbeleg: false);
            SeedTenant(db, TenantB, "Beta", "AT", hasJahresbeleg: true);
            SeedTenant(db, TenantDe, "Delta", "DE", hasJahresbeleg: false);
            await db.SaveChangesAsync();

            var sent = await sut.CheckAndNotifyAsync();

            Assert.Equal(1, sent);
            var evt = Assert.Single(published);
            Assert.Equal(TenantA, evt.TenantId);
            Assert.Equal(ActivityEventType.JahresbelegDeadlineReminder, evt.Type);
            Assert.Equal(JahresbelegDeadline.WarningMessage, evt.Description);

            var again = await sut.CheckAndNotifyAsync();
            Assert.Equal(0, again);
            Assert.Single(published);
        }
    }

    [Fact]
    public async Task CheckAndNotify_BeforeDecember_SendsNothing()
    {
        var (sut, published, db) = CreateSut(new DateTime(2026, 11, 30, 10, 0, 0, DateTimeKind.Utc));
        await using (db)
        {
            SeedTenant(db, TenantA, "Alpha", "AT", hasJahresbeleg: false);
            await db.SaveChangesAsync();

            var sent = await sut.CheckAndNotifyAsync();

            Assert.Equal(0, sent);
            Assert.Empty(published);
        }
    }

    [Fact]
    public async Task GetStatus_OnDecemberFirst_MarksMissingAndCreated()
    {
        var (sut, _, db) = CreateSut(DecemberFirstUtc);
        await using (db)
        {
            SeedTenant(db, TenantA, "Alpha", "AT", hasJahresbeleg: false);
            SeedTenant(db, TenantB, "Beta", "AT", hasJahresbeleg: true);
            await db.SaveChangesAsync();

            var rows = await sut.GetStatusAsync(null);

            Assert.Equal(2, rows.Count);
            Assert.Equal(JahresbelegReminderService.StatusMissing, rows.Single(r => r.TenantId == TenantA).Status);
            Assert.Equal(JahresbelegDeadline.WarningMessage, rows.Single(r => r.TenantId == TenantA).Warning);
            Assert.Equal(JahresbelegReminderService.StatusCreated, rows.Single(r => r.TenantId == TenantB).Status);
            Assert.Equal(new DateOnly(2027, 1, 31), rows[0].Deadline);
        }
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId, string name, string country, bool hasJahresbeleg)
    {
        TenantTestDoubles.EnsureTenant(db, tenantId, name.ToLowerInvariant());
        var tenant = db.Tenants.Local.First(t => t.Id == tenantId);
        tenant.Name = name;
        tenant.Status = TenantStatuses.Active;

        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = name,
            CompanyAddress = "Street 1",
            CompanyTaxNumber = "ATU12345678",
            Country = country,
            Language = "de",
            TimeZone = "Europe/Vienna",
        });

        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = name,
            Location = "Lab",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DecemberFirstUtc,
            Status = RegisterStatus.Open,
            CreatedAt = DecemberFirstUtc,
            IsActive = true,
        });

        if (!hasJahresbeleg)
            return;

        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "s",
            TseTimestamp = DecemberFirstUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = $"J-{name}",
            CreatedAt = DecemberFirstUtc,
            CreatedBy = "u",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Jahresbeleg,
            RksvSpecialReceiptYear = 2026,
        });
    }

    private static (JahresbelegReminderService Sut, List<ActivityEventPublishRequest> Published, AppDbContext Db)
        CreateSut(DateTime utcNow)
    {
        var accessor = new MutableAccessor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, accessor);
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.SaveChanges();

        var published = new List<ActivityEventPublishRequest>();
        var activity = new Mock<IActivityEventService>();
        activity
            .Setup(a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityEventPublishRequest, CancellationToken>((req, _) =>
            {
                published.Add(req);
                db.ActivityEvents.Add(new ActivityEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = req.TenantId,
                    Type = req.Type,
                    Title = req.Title,
                    Description = req.Description,
                    DedupKey = req.DedupKey,
                    Severity = ActivitySeverityNames.Warning,
                    CreatedAtUtc = utcNow,
                });
                db.SaveChanges();
            })
            .ReturnsAsync(new ActivityEvent());

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentTenantAccessor>(accessor);
        services.AddSingleton(db);
        services.AddSingleton(activity.Object);
        _ = services.BuildServiceProvider();

        var sut = new JahresbelegReminderService(
            db,
            activity.Object,
            accessor,
            new FixedClock(utcNow),
            NullLogger<JahresbelegReminderService>.Instance);
        return (sut, published, db);
    }

    private sealed class MutableAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; }
        public string? TenantSlug { get; set; }
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
