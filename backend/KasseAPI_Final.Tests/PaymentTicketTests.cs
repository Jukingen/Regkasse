using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
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
        Assert.False(string.Equals(result.QrPayload, issued.Code, StringComparison.Ordinal));
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
