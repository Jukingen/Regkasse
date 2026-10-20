using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentTicketTests
{
    [Fact]
    public async Task CreatePayment_IssuesHashedTicket_WhenTicketSalesProduct()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.IsTicket = true;
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { VerticalProfiles = Profile(ticketSales: true) });
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Single(result.IssuedTickets);
        var issued = result.IssuedTickets[0];
        Assert.StartsWith("TKT-", issued.Code, StringComparison.Ordinal);
        var row = await ctx.TicketRedemptions.AsNoTracking().SingleAsync();
        Assert.Equal(TicketCodeHasher.HashRaw(issued.Code), row.TicketCodeHash);
        Assert.Equal(TicketCodeHasher.DisplayPrefix(row.TicketCodeHash), row.TicketCode);
        Assert.NotEqual(issued.Code, row.TicketCode);
        Assert.Equal(TicketRedemptionStatus.Valid, row.Status);
        Assert.Equal(result.PaymentId, row.PaymentDetailId);
        Assert.True(row.ValidUntilUtc > DateTime.UtcNow.AddDays(360));
        // v2 seat categories stay off this path: the RKSV QR payload must never be the ticket code.
        Assert.False(string.Equals(result.QrPayload, issued.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryPricedSeatSell_DoesNotWriteTicketCodeIntoPaymentQr()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.IsTicket = true;
        await ctx.SaveChangesAsync();

        var tenantId = SystemTenantIds.Platform;
        var events = new TicketEventService(ctx, TicketAuditTestDoubles.Loose().Object, NullLogger<TicketEventService>.Instance);
        var created = await events.CreateAsync(tenantId, new UpsertTicketEventRequest
        {
            Name = "Kammerkonzert",
            Venue = "Saal 1",
            Capacity = 20,
            IsActive = true,
            StartAtUtc = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
        }, CancellationToken.None);
        var category = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                tenantId,
                new UpsertTicketPriceCategoryRequest { Code = "standard", Name = "Standard" },
                "manager-1",
                CancellationToken.None));
        Assert.IsType<TicketCatalogResult.PriceOk>(
            await events.SetCategoryPriceAsync(
                created!.Id,
                new SetTicketEventCategoryPriceRequest { CategoryId = category.Category.Id, Price = 25m, Capacity = 4 },
                "manager-1",
                CancellationToken.None));
        var seat = Assert.IsType<TicketSeatActionResult.OkSeat>(
            await events.AddSeatAsync(
                created.Id,
                new CreateTicketSeatRequest { Row = "A", Number = "1", CategoryId = category.Category.Id, GridColumn = 1 },
                CancellationToken.None));
        var sold = Assert.IsType<TicketSeatActionResult.OkSale>(
            await events.SellAsync(tenantId, created.Id, seat.Seat.Id, "cashier-seat", CancellationToken.None));
        Assert.Equal(25m, sold.Ticket.Price);
        var seatRedemption = await ctx.TicketRedemptions.SingleAsync(row => row.SeatId == seat.Seat.Id);
        Assert.Null(seatRedemption.PaymentDetailId);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { VerticalProfiles = Profile(ticketSales: true) });
        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.False(string.Equals(result.QrPayload, sold.Ticket.Code, StringComparison.Ordinal));
        Assert.DoesNotContain(sold.Ticket.Code, result.QrPayload ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePayment_RejectsTicketProduct_WhenProfileIsNotTicketSales()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.IsTicket = true;
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { VerticalProfiles = Profile(ticketSales: false) });
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("PROFILE_FEATURE_DISABLED", result.DiagnosticCode);
        Assert.Equal("ticketScan", result.ProfileFeature);
        Assert.Empty(result.IssuedTickets);
        Assert.Equal(0, await ctx.TicketRedemptions.CountAsync());
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    private static IVerticalProfileService Profile(bool ticketSales)
    {
        var mock = new Mock<IVerticalProfileService>();
        mock.Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveVerticalProfileDto(
                ticketSales ? VerticalProfileIds.TicketSales : VerticalProfileIds.Gastronomy,
                "test",
                JsonSerializer.SerializeToElement(new Dictionary<string, bool>
                {
                    ["ticketScan"] = ticketSales,
                    ["kitchenDisplay"] = false,
                }),
                JsonDocument.Parse("{}").RootElement.Clone(),
                JsonDocument.Parse("{}").RootElement.Clone(),
                ticketSales ? VerticalProfileLayouts.Ticket : VerticalProfileLayouts.Standard,
                JsonDocument.Parse("{}").RootElement.Clone()));
        return mock.Object;
    }
}
