using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentTaxiTests
{
    [Fact]
    public async Task CreatePayment_PersistsTaxiRouteFields_WhenProfileIsTaxi()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = Profile(taxi: true),
            });

        var started = DateTime.UtcNow.AddMinutes(-12);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.RouteFrom = " Hauptbahnhof ";
        request.RouteTo = "Flughafen";
        request.RouteKm = 18.50m;
        request.TripStartedAtUtc = started;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var payment = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        Assert.Equal("Hauptbahnhof", payment.RouteFrom);
        Assert.Equal("Flughafen", payment.RouteTo);
        Assert.Equal(18.50m, payment.RouteKm);
        Assert.Equal(started, payment.TripStartedAtUtc);
    }

    [Fact]
    public async Task CreatePayment_RejectsTaxiFields_WhenProfileIsNotTaxi()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = Profile(taxi: false),
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.RouteFrom = "A";
        request.RouteTo = "B";
        request.RouteKm = 3m;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("TAXI_FIELDS_NOT_ALLOWED", result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    private static IVerticalProfileService Profile(bool taxi)
    {
        var mock = new Mock<IVerticalProfileService>();
        mock.Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveVerticalProfileDto(
                taxi ? VerticalProfileIds.Taxi : VerticalProfileIds.Gastronomy,
                "test",
                JsonSerializer.SerializeToElement(new Dictionary<string, bool>
                {
                    ["routeTracking"] = taxi,
                }),
                JsonDocument.Parse("{}").RootElement.Clone(),
                JsonDocument.Parse("{}").RootElement.Clone(),
                taxi ? VerticalProfileLayouts.Taxi : VerticalProfileLayouts.Standard,
                JsonDocument.Parse("{}").RootElement.Clone(),
                taxi ? 2.40m : null));
        return mock.Object;
    }
}
