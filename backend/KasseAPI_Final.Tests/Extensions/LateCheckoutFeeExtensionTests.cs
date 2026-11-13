using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Extensions;

public sealed class LateCheckoutFeeExtensionTests
{
    private static readonly DateTimeOffset BeforeFreeUntil =
        new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset AfterFreeUntil =
        new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void AppliesTo_WithoutBeherbergung_IsFalse()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(tenantId);
        var sut = CreateExtension(ctx, "gastronomy", BeforeFreeUntil);

        Assert.False(sut.AppliesTo(tenantId));
    }

    [Fact]
    public async Task Execute_BeforeFreeUntil_WritesNothing()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        SeedPolicy(ctx, tenantId);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung, BeforeFreeUntil);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Execute_AfterFreeUntil_PostsOneDeferredLine()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        SeedPolicy(ctx, tenantId);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung, AfterFreeUntil);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        var item = Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        Assert.Null(item.PaymentDetailId);
        Assert.Equal("Später Check-out", item.Description);
        Assert.Equal(30.00m, item.Amount);
        Assert.Equal(30.00m, (await ctx.GuestFolios.SingleAsync()).Balance);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task Execute_WithoutOptions_IsNoOp()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung, AfterFreeUntil);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Execute_SecondCall_DoesNotDoubleCharge()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        SeedPolicy(ctx, tenantId);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung, AfterFreeUntil);

        Assert.True((await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None)).Success);
        await ctx.SaveChangesAsync();
        Assert.True((await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None)).Success);
        await ctx.SaveChangesAsync();

        Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        Assert.Equal(30.00m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    private static LateCheckoutFeeExtension CreateExtension(
        AppDbContext ctx,
        string profileId,
        DateTimeOffset utcNow)
    {
        var profiles = new Mock<IVerticalProfileService>();
        profiles
            .Setup(service => service.GetForAdminTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveVerticalProfileDto(
                profileId,
                profileId,
                JsonSerializer.SerializeToElement(new Dictionary<string, bool>()),
                default,
                default,
                "rooms",
                default));
        return new LateCheckoutFeeExtension(ctx, profiles.Object, new FixedClock(utcNow));
    }

    private static ExtensionContext Context(Guid tenantId, GuestFolio folio) =>
        new(
            tenantId,
            TenantExtensionHooks.FolioBeforeClose,
            new Dictionary<string, object?>
            {
                ["folioId"] = folio.Id,
                ["roomId"] = folio.RoomId,
            });

    private static async Task<GuestFolio> SeedFolioAsync(AppDbContext ctx, Guid tenantId)
    {
        var room = new Room
        {
            TenantId = tenantId,
            Number = "8",
            Type = "EZ",
            Capacity = 1,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = "Gast",
            CustomerNumber = $"C-{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Rooms.Add(room);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            RoomId = room.Id,
            CheckIn = new DateTime(2026, 1, 14, 15, 0, 0, DateTimeKind.Utc),
            Status = GuestFolioStatus.Open,
            Balance = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        ctx.GuestFolios.Add(folio);
        await ctx.SaveChangesAsync();
        return folio;
    }

    private static void SeedPolicy(AppDbContext ctx, Guid tenantId)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = LateCheckoutFeePolicy.SettingsKey,
            Value = """{"freeUntilLocalTime":"11:00","fee":30.00,"timeZone":"Europe/Vienna"}""",
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LateCheckout_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        return ctx;
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
