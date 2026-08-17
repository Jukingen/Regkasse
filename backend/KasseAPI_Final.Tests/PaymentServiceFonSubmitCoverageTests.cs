using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Coverage for <see cref="PaymentService.SendToFinanzOnlineAsync"/> (submit, missing TSE, missing register, FON fail, exception).
/// </summary>
public sealed class PaymentServiceFonSubmitCoverageTests
{
    [Fact]
    public async Task SendToFinanzOnline_WhenNoTseSignature_ReturnsFalse()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var payment = MinimalPayment(Guid.NewGuid(), tseSignature: string.Empty);

        var sent = await sut.SendToFinanzOnlineAsync(payment);

        Assert.False(sent);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenSubmitSucceeds_ReturnsTrue()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var registerNumber = (await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == registerId)).RegisterNumber;
        var finanz = new Mock<IFinanzOnlineService>();
        Invoice? submitted = null;
        finanz.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice invoice) =>
            {
                submitted = invoice;
                return new FinanzOnlineSubmitResponse { Success = true, Status = "Submitted", ReferenceId = "FON-OK" };
            });
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Finanz = finanz });
        var payment = MinimalPayment(registerId);

        var sent = await sut.SendToFinanzOnlineAsync(payment);

        Assert.True(sent);
        Assert.NotNull(submitted);
        Assert.Equal(registerNumber, submitted!.KassenId);
        Assert.Equal(payment.TseSignature, submitted.TseSignature);
        Assert.Equal(payment.TotalAmount, submitted.TotalAmount);
        finanz.Verify(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()), Times.Once);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenSubmitFails_ReturnsFalse()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var finanz = new Mock<IFinanzOnlineService>();
        finanz.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse
            {
                Success = false,
                Status = "Failed",
                ErrorMessage = "FON rejected"
            });
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Finanz = finanz });

        var sent = await sut.SendToFinanzOnlineAsync(MinimalPayment(registerId));

        Assert.False(sent);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenCashRegisterMissing_ReturnsFalse()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var payment = MinimalPayment(Guid.NewGuid());

        var sent = await sut.SendToFinanzOnlineAsync(payment);

        Assert.False(sent);
    }

    [Fact]
    public async Task SendToFinanzOnline_WhenFinanzOnlineThrows_ReturnsFalse()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var finanz = new Mock<IFinanzOnlineService>();
        finanz.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ThrowsAsync(new InvalidOperationException("FON SOAP timeout"));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Finanz = finanz });

        var sent = await sut.SendToFinanzOnlineAsync(MinimalPayment(registerId));

        Assert.False(sent);
    }

    private static PaymentDetails MinimalPayment(Guid cashRegisterId, string tseSignature = "eyJ.eyJ.sig") =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            CashRegisterId = cashRegisterId,
            TotalAmount = 10m,
            TaxAmount = 0.91m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CompanyName = "Test GmbH",
            TseSignature = tseSignature,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
}
