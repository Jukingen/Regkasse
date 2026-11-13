using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Extensions;

public sealed class MinibarAutoChargeExtensionTests
{
    [Fact]
    public void AppliesTo_WithoutBeherbergung_IsFalse()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(tenantId);
        var sut = CreateExtension(ctx, out _, profileId: "gastronomy");

        Assert.False(sut.AppliesTo(tenantId));
    }

    [Fact]
    public async Task Execute_NoPendingMinibar_ReturnsSuccessWithoutRows()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        var sut = CreateExtension(ctx, out var audit, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        audit.Verify(
            service => service.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.FolioCharged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_PendingRowsWithoutSettlementRequirement_ChargesFolio()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        SeedPolicy(ctx, tenantId, requireSettlement: false);
        await SeedMinibarAsync(ctx, tenantId, folio.RoomId, quantity: 2, unitPrice: 4.50m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, out var audit, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);
        await ctx.SaveChangesAsync();

        Assert.True(result.Success);
        var item = Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        Assert.Null(item.PaymentDetailId);
        Assert.Equal(9.00m, item.Amount);
        Assert.Equal(9.00m, (await ctx.GuestFolios.SingleAsync()).Balance);
        Assert.Equal(RoomMinibarItemStatus.Charged, (await ctx.RoomMinibarItems.SingleAsync()).Status);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        audit.Verify(
            service => service.LogSystemOperationAsync(
                "FOLIO_CHARGED",
                "GuestFolio",
                "system",
                "Cashier",
                It.Is<string?>(text => text != null && text.Contains("No payment row and no TSE receipt", StringComparison.Ordinal)),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.FolioCharged,
                folio.Id,
                tenantId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_PendingRowsWithSettlementRequirement_RejectsWithoutChanges()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        await SeedMinibarAsync(ctx, tenantId, folio.RoomId, quantity: 1, unitPrice: 3m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, out _, VerticalProfileIds.Beherbergung);

        var result = await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(MinibarAutoChargePolicy.RejectionCode, result.Message);
        Assert.Equal(RoomMinibarItemStatus.Pending, (await ctx.RoomMinibarItems.SingleAsync()).Status);
        Assert.Equal(0, await ctx.GuestFolioItems.CountAsync());
        Assert.Equal(0m, (await ctx.GuestFolios.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Execute_SecondCall_DoesNotDoubleCharge()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var folio = await SeedFolioAsync(ctx, tenantId);
        SeedPolicy(ctx, tenantId, requireSettlement: false);
        await SeedMinibarAsync(ctx, tenantId, folio.RoomId, quantity: 1, unitPrice: 5m);
        await ctx.SaveChangesAsync();
        var sut = CreateExtension(ctx, out var audit, VerticalProfileIds.Beherbergung);

        Assert.True((await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None)).Success);
        await ctx.SaveChangesAsync();
        Assert.True((await sut.ExecuteAsync(Context(tenantId, folio), CancellationToken.None)).Success);
        await ctx.SaveChangesAsync();

        Assert.Equal(5.00m, (await ctx.GuestFolios.SingleAsync()).Balance);
        Assert.Single(await ctx.GuestFolioItems.ToListAsync());
        audit.Verify(
            service => service.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.FolioCharged,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    private static MinibarAutoChargeExtension CreateExtension(
        AppDbContext ctx,
        out Mock<IAuditLogService> audit,
        string profileId)
    {
        audit = new Mock<IAuditLogService>();
        audit.Setup(service => service.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
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
        return new MinibarAutoChargeExtension(ctx, profiles.Object, audit.Object);
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
        var folio = new GuestFolio
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            RoomId = room.Id,
            CheckIn = DateTime.UtcNow,
            Status = GuestFolioStatus.Open,
            Balance = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        ctx.GuestFolios.Add(folio);
        await ctx.SaveChangesAsync();
        return folio;
    }

    private static Task SeedMinibarAsync(
        AppDbContext ctx,
        Guid tenantId,
        Guid roomId,
        int quantity,
        decimal unitPrice)
    {
        ctx.RoomMinibarItems.Add(new RoomMinibarItem
        {
            TenantId = tenantId,
            RoomId = roomId,
            Description = "Water",
            Quantity = quantity,
            UnitPrice = unitPrice,
            Status = RoomMinibarItemStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        return Task.CompletedTask;
    }

    private static void SeedPolicy(AppDbContext ctx, Guid tenantId, bool requireSettlement)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = MinibarAutoChargePolicy.SettingsKey,
            Value = "{\"requireSettlementBeforeClose\":" + (requireSettlement ? "true" : "false") + "}",
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Minibar_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        return ctx;
    }
}
