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

public sealed class BreakfastIncludedExtensionTests
{
    [Fact]
    public void AppliesTo_WithoutBeherbergung_IsFalse()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(tenantId);
        var sut = CreateExtension(ctx, "gastronomy");

        Assert.False(sut.AppliesTo(tenantId));
    }

    [Fact]
    public async Task Execute_NotesWithoutBreakfast_WritesNothing()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId, notes: "late arrival", nights: 2);
        SeedPrice(ctx, tenantId, 12.50m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Execute_BreakfastWithoutOptions_IsNoOp()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId, notes: "Breakfast included", nights: 2);
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Execute_BreakfastWithPrice_PostsOneDeferredLine()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId, notes: "breakfast for the stay", nights: 1);
        SeedPrice(ctx, tenantId, 12.50m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        var item = Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        Assert.Null(item.PaymentDetailId);
        Assert.Equal("Frühstück (1 Nächte)", item.Description);
        Assert.Equal(12.50m, item.Amount);
        Assert.Equal(12.50m, (await ctx.GuestFolios.SingleAsync()).Balance);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task Execute_MultipleNights_MultipliesPrice()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId, notes: "BREAKFAST", nights: 3);
        SeedPrice(ctx, tenantId, 12.50m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        var item = Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        Assert.Equal("Frühstück (3 Nächte)", item.Description);
        Assert.Equal(37.50m, item.Amount);
        Assert.Equal(37.50m, (await ctx.GuestFolios.SingleAsync()).Balance);
        Assert.Null(item.PaymentDetailId);
    }

    private static BreakfastIncludedExtension CreateExtension(AppDbContext ctx, string profileId)
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
        return new BreakfastIncludedExtension(ctx, profiles.Object);
    }

    private static ExtensionContext Context(Guid tenantId, GuestFolio folio) =>
        new(
            tenantId,
            TenantExtensionHooks.FolioOpened,
            new Dictionary<string, object?>
            {
                ["folioId"] = folio.Id,
                ["roomId"] = folio.RoomId,
            });

    private static async Task<GuestFolio> SeedFolioAsync(
        AppDbContext ctx,
        Guid tenantId,
        string? notes,
        int nights)
    {
        var room = new Room
        {
            TenantId = tenantId,
            Number = "12",
            Type = "DZ",
            Capacity = 2,
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
        var checkIn = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            RoomId = room.Id,
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(nights),
            Notes = notes,
            Status = GuestFolioStatus.Open,
            Balance = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        ctx.GuestFolios.Add(folio);
        await ctx.SaveChangesAsync();
        return folio;
    }

    private static void SeedPrice(AppDbContext ctx, Guid tenantId, decimal price)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = BreakfastIncludedPolicy.SettingsKey,
            Value = "{\"pricePerPersonPerNight\":" + price.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}",
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Breakfast_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        return ctx;
    }
}
