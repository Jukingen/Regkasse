using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.PaymentGateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MockCardGatewayTests
{
    private static MockCardGateway CreateGateway() =>
        new(NullLogger<MockCardGateway>.Instance, Options.Create(new PaymentGatewayOptions { SimulateDelayMs = 0 }));

    [Theory]
    [InlineData("4242424242424242", true, "Visa")]
    [InlineData("5555555555554444", true, "Mastercard")]
    [InlineData("4000000000000002", false, "Visa")]
    [InlineData("4000000000009995", false, "Visa")]
    public void EvaluateTestCard_returnsExpectedOutcome(string cardNumber, bool expectedSuccess, string expectedBrand)
    {
        var (success, error, brand) = MockCardGateway.EvaluateTestCard(cardNumber);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedBrand, brand);
        if (!expectedSuccess)
            Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task CreatePaymentIntent_returnsCreatedStatus_andStoresIntent()
    {
        var gateway = CreateGateway();
        var intentId = Guid.NewGuid();

        var result = await gateway.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
        {
            InternalIntentId = intentId,
            Amount = 12.50m,
            Currency = "EUR"
        });

        Assert.True(result.Success);
        Assert.Equal(PaymentIntentStatus.Created, result.Status);
        Assert.Equal(intentId.ToString("D"), result.PaymentIntentId);
        Assert.False(string.IsNullOrWhiteSpace(result.ClientSecret));
        Assert.StartsWith("MOCK_TXN_", result.TransactionId);

        var status = await gateway.GetPaymentStatusAsync(result.PaymentIntentId);
        Assert.Equal(PaymentIntentStatus.Created, status);
    }

    [Fact]
    public async Task ConfirmPayment_withoutCreate_returnsFailed()
    {
        var gateway = CreateGateway();
        var result = await gateway.ConfirmPaymentAsync(Guid.NewGuid().ToString("D"), "4242424242424242");

        Assert.False(result.Success);
        Assert.Equal(PaymentIntentStatus.Failed, result.Status);
        Assert.Equal("Payment intent not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmPayment_successCard_returnsSucceeded()
    {
        var gateway = CreateGateway();
        var intentId = Guid.NewGuid();

        var created = await gateway.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
        {
            InternalIntentId = intentId,
            Amount = 25.00m
        });

        var result = await gateway.ConfirmPaymentAsync(created.PaymentIntentId, "4242424242424242");

        Assert.True(result.Success);
        Assert.Equal(PaymentIntentStatus.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.TransactionId));
        Assert.Equal("4242", result.LastFourDigits);
    }

    [Fact]
    public async Task ConfirmPayment_declinedCard_returnsFailed()
    {
        var gateway = CreateGateway();
        var intentId = Guid.NewGuid();

        var created = await gateway.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
        {
            InternalIntentId = intentId,
            Amount = 25.00m
        });

        var result = await gateway.ConfirmPaymentAsync(created.PaymentIntentId, "4000000000000002");

        Assert.False(result.Success);
        Assert.Equal(PaymentIntentStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmPayment_declineAmount_withoutCardNumber_returnsFailed()
    {
        var gateway = CreateGateway();
        var intentId = Guid.NewGuid();

        var created = await gateway.CreatePaymentIntentAsync(new CreatePaymentIntentRequest
        {
            InternalIntentId = intentId,
            Amount = MockCardGateway.SimulatedDeclineAmount
        });

        var result = await gateway.ConfirmPaymentAsync(created.PaymentIntentId, paymentMethodId: null);

        Assert.False(result.Success);
        Assert.Equal(PaymentIntentStatus.Failed, result.Status);
    }
}
