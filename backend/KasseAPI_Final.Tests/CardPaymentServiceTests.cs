using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Dedicated coverage for <see cref="CardPaymentService"/> (intent create/confirm/cancel, fiscal validate, link).
/// </summary>
public sealed class CardPaymentServiceTests
{
    private const string UserId = "cashier1";

    [Fact]
    public async Task CreateIntent_WhenAmountInvalid_ReturnsInvalidAmount()
    {
        var (sut, _, _, _) = CreateSut();

        var (response, code, message) = await sut.CreateIntentAsync(
            new CreateCardPaymentIntentRequest { Amount = 0m, CashRegisterId = Guid.NewGuid() },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_INVALID_AMOUNT", code);
        Assert.Contains("greater than zero", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateIntent_WhenRegisterInvalid_ReturnsRegisterCode()
    {
        var registerId = Guid.NewGuid();
        var (sut, _, resolution, _) = CreateSut();
        resolution.Setup(r => r.ValidatePaymentRegisterForCommitAsync(
                UserId, registerId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Failure("REGISTER_CLOSED", "Kasse geschlossen"));

        var (response, code, message) = await sut.CreateIntentAsync(
            new CreateCardPaymentIntentRequest { Amount = 10m, CashRegisterId = registerId },
            UserId);

        Assert.Null(response);
        Assert.Equal("REGISTER_CLOSED", code);
        Assert.Equal("Kasse geschlossen", message);
    }

    [Fact]
    public async Task CreateIntent_WhenGatewayThrows_ReturnsGatewayError()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gateway down"));

        var (response, code, _) = await sut.CreateIntentAsync(
            new CreateCardPaymentIntentRequest { Amount = 10m, CashRegisterId = registerId },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_GATEWAY_ERROR", code);
    }

    [Fact]
    public async Task CreateIntent_WhenGatewayFails_ReturnsCreateFailed()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult { Success = false, ErrorMessage = "card declined at create" });

        var (response, code, message) = await sut.CreateIntentAsync(
            new CreateCardPaymentIntentRequest { Amount = 10m, CashRegisterId = registerId },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_CREATE_FAILED", code);
        Assert.Equal("card declined at create", message);
    }

    [Fact]
    public async Task CreateIntent_WhenGatewaySucceeds_PersistsRow()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, ctx) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_create", PaymentIntentStatus.Created, clientSecret: "secret"));

        var (response, code, _) = await sut.CreateIntentAsync(
            new CreateCardPaymentIntentRequest
            {
                Amount = 12.50m,
                Currency = "eur",
                CashRegisterId = registerId,
                Description = "Tisch 1",
                Metadata = new Dictionary<string, string> { ["table"] = "1" }
            },
            UserId);

        Assert.Null(code);
        Assert.NotNull(response);
        Assert.Equal(12.50m, response!.Amount);
        Assert.Equal("EUR", response.Currency);
        Assert.Equal(CardPaymentTransactionStatuses.Created, response.Status);
        Assert.Equal("secret", response.ClientSecret);
        Assert.Equal(registerId, response.CashRegisterId);
        Assert.Equal(1, await ctx.CardPaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task CreateIntentFromPosRequest_WhenReceiptPresent_AddsMetadata()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        CreatePaymentIntentRequest? captured = null;
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePaymentIntentRequest req, CancellationToken _) =>
            {
                captured = req;
                return OkIntent("pi_pos", PaymentIntentStatus.Pending);
            });

        var (response, code, _) = await sut.CreateIntentFromPosRequestAsync(
            new CardPaymentRequest { Amount = 10m, CashRegisterId = registerId, ReceiptNumber = "AT-1" },
            UserId);

        Assert.Null(code);
        Assert.NotNull(response);
        Assert.NotNull(captured);
        Assert.Equal("Payment for receipt AT-1", captured!.Description);
        Assert.Equal("AT-1", captured.Metadata["receiptNumber"]);
        Assert.Equal(registerId.ToString(), captured.Metadata["cashRegisterId"]);
    }

    [Fact]
    public async Task ConfirmIntent_WhenNotFound_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut();

        var (response, code, _) = await sut.ConfirmIntentAsync(
            Guid.NewGuid(),
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = "pm_1" },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_NOT_FOUND", code);
    }

    [Fact]
    public async Task ConfirmIntent_WhenAlreadySucceeded_IsIdempotent()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded);

        var (response, code, _) = await sut.ConfirmIntentAsync(
            row.Id,
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = "pm_1" },
            UserId);

        Assert.Null(code);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, response!.Status);
        gateway.Verify(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmIntent_WhenGatewayThrows_ReturnsGatewayError()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);
        gateway.Setup(g => g.ConfirmPaymentAsync(row.GatewayPaymentIntentId!, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("timeout"));

        var (response, code, _) = await sut.ConfirmIntentAsync(
            row.Id,
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = "pm_1" },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_GATEWAY_ERROR", code);
    }

    [Fact]
    public async Task ConfirmIntent_WhenGatewayDeclines_ReturnsDeclined()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);
        gateway.Setup(g => g.ConfirmPaymentAsync(row.GatewayPaymentIntentId!, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult
            {
                Success = false,
                Status = PaymentIntentStatus.Failed,
                ErrorMessage = "insufficient funds",
                TransactionId = "ch_fail"
            });

        var (response, code, message) = await sut.ConfirmIntentAsync(
            row.Id,
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = "pm_1" },
            UserId);

        Assert.Equal("CARD_CONFIRM_DECLINED", code);
        Assert.Equal("insufficient funds", message);
        Assert.Equal(CardPaymentTransactionStatuses.Failed, response!.Status);
        var stored = await ctx.CardPaymentTransactions.AsNoTracking().FirstAsync(c => c.Id == row.Id);
        Assert.Equal(CardPaymentTransactionStatuses.Failed, stored.Status);
    }

    [Fact]
    public async Task ConfirmIntent_WhenGatewaySucceeds_MarksSucceeded()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);
        gateway.Setup(g => g.ConfirmPaymentAsync(row.GatewayPaymentIntentId!, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult
            {
                Success = true,
                Status = PaymentIntentStatus.Succeeded,
                TransactionId = "ch_ok",
                CardBrand = "visa",
                LastFourDigits = "4242"
            });

        var (response, code, _) = await sut.ConfirmIntentAsync(
            row.Id,
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = "pm_1" },
            UserId);

        Assert.Null(code);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, response!.Status);
        Assert.Equal("visa", response.CardBrand);
        Assert.Equal("4242", response.LastFourDigits);
        Assert.NotNull(response.ConfirmedAtUtc);
    }

    [Fact]
    public async Task ConfirmByPaymentIntentId_WhenBlank_ReturnsRequired()
    {
        var (sut, _, _, _) = CreateSut();

        var (response, code, _) = await sut.ConfirmByPaymentIntentIdAsync(
            new ConfirmCardPaymentRequest { PaymentIntentId = "  " },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_ID_REQUIRED", code);
    }

    [Fact]
    public async Task ConfirmByPaymentIntentId_WhenUnknown_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut();

        var (response, code, _) = await sut.ConfirmByPaymentIntentIdAsync(
            new ConfirmCardPaymentRequest { PaymentIntentId = "pi_missing" },
            UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_NOT_FOUND", code);
    }

    [Fact]
    public async Task ConfirmByPaymentIntentId_WhenGatewayId_Confirms()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created, gatewayIntentId: "pi_gateway");
        gateway.Setup(g => g.ConfirmPaymentAsync("pi_gateway", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult { Success = true, Status = PaymentIntentStatus.Succeeded, TransactionId = "ch_g" });

        var (response, code, _) = await sut.ConfirmByPaymentIntentIdAsync(
            new ConfirmCardPaymentRequest { PaymentIntentId = "pi_gateway", PaymentMethodId = "pm_1" },
            UserId);

        Assert.Null(code);
        Assert.True(response!.Success);
        Assert.Equal(row.Id, response.TransactionId);
    }

    [Fact]
    public async Task CancelIntent_WhenSucceeded_ReturnsAlreadySucceeded()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded);

        var (response, code, _) = await sut.CancelIntentAsync(row.Id, UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_ALREADY_SUCCEEDED", code);
    }

    [Fact]
    public async Task CancelIntent_WhenGatewayThrows_ReturnsGatewayError()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);
        gateway.Setup(g => g.CancelPaymentAsync(row.GatewayPaymentIntentId!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cancel failed"));

        var (response, code, _) = await sut.CancelIntentAsync(row.Id, UserId);

        Assert.Null(response);
        Assert.Equal("CARD_GATEWAY_ERROR", code);
    }

    [Fact]
    public async Task CancelIntent_WhenGatewaySucceeds_MarksCancelled()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);
        gateway.Setup(g => g.CancelPaymentAsync(row.GatewayPaymentIntentId!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult { Success = true, Status = PaymentIntentStatus.Cancelled });

        var (response, code, _) = await sut.CancelIntentAsync(row.Id, UserId);

        Assert.Null(code);
        Assert.Equal(CardPaymentTransactionStatuses.Cancelled, response!.Status);
    }

    [Fact]
    public async Task CancelIntent_WhenNotFound_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut();

        var (response, code, _) = await sut.CancelIntentAsync(Guid.NewGuid(), UserId);

        Assert.Null(response);
        Assert.Equal("CARD_INTENT_NOT_FOUND", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenRequireCardIntentOff_ReturnsOkWithoutRow()
    {
        var (sut, _, _, _) = CreateSut(requireCardIntent: false);

        var (ok, row, code, _) = await sut.ValidateForFiscalPaymentAsync(Guid.Empty, 10m, Guid.NewGuid());

        Assert.True(ok);
        Assert.Null(row);
        Assert.Null(code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenIntentEmpty_ReturnsRequired()
    {
        var (sut, _, _, _) = CreateSut(requireCardIntent: true);

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(Guid.Empty, 10m, Guid.NewGuid());

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_REQUIRED", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenNotFound_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut(requireCardIntent: true);

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(Guid.NewGuid(), 10m, Guid.NewGuid());

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_NOT_FOUND", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenNotConfirmed_ReturnsNotConfirmed()
    {
        var (sut, _, _, ctx) = CreateSut(requireCardIntent: true);
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Created);

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(row.Id, 10m, row.CashRegisterId);

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_NOT_CONFIRMED", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenAlreadyUsed_ReturnsAlreadyUsed()
    {
        var (sut, _, _, ctx) = CreateSut(requireCardIntent: true);
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded, paymentId: Guid.NewGuid());

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(row.Id, 10m, row.CashRegisterId);

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_ALREADY_USED", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenRegisterMismatch_ReturnsMismatch()
    {
        var (sut, _, _, ctx) = CreateSut(requireCardIntent: true);
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded);

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(row.Id, 10m, Guid.NewGuid());

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_REGISTER_MISMATCH", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenAmountMismatch_ReturnsMismatch()
    {
        var (sut, _, _, ctx) = CreateSut(requireCardIntent: true);
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded, amount: 10m);

        var (ok, _, code, _) = await sut.ValidateForFiscalPaymentAsync(row.Id, 20m, row.CashRegisterId);

        Assert.False(ok);
        Assert.Equal("CARD_INTENT_AMOUNT_MISMATCH", code);
    }

    [Fact]
    public async Task ValidateForFiscalPayment_WhenConfirmedUnusedMatching_ReturnsOk()
    {
        var (sut, _, _, ctx) = CreateSut(requireCardIntent: true);
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded, amount: 10m);

        var (ok, found, code, _) = await sut.ValidateForFiscalPaymentAsync(row.Id, 10.005m, row.CashRegisterId);

        Assert.True(ok);
        Assert.Null(code);
        Assert.Equal(row.Id, found!.Id);
    }

    [Fact]
    public async Task LinkToPayment_WhenIntentExists_SetsPaymentId()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = await SeedIntentAsync(ctx, CardPaymentTransactionStatuses.Succeeded);
        var paymentId = Guid.NewGuid();

        await sut.LinkToPaymentAsync(row.Id, paymentId);

        var stored = await ctx.CardPaymentTransactions.AsNoTracking().FirstAsync(c => c.Id == row.Id);
        Assert.Equal(paymentId, stored.PaymentId);
    }

    [Fact]
    public async Task LinkToPayment_WhenIntentMissing_DoesNotThrow()
    {
        var (sut, _, _, _) = CreateSut();

        await sut.LinkToPaymentAsync(Guid.NewGuid(), Guid.NewGuid());
    }

    private static (CardPaymentService Sut, Mock<IPaymentGateway> Gateway, Mock<ICashRegisterResolutionService> Resolution, AppDbContext Ctx)
        CreateSut(bool requireCardIntent = false)
    {
        var ctx = PaymentServiceCoverageHarness.CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        ctx.SaveChanges();

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(g => g.ProviderName).Returns("Mock");

        var resolution = new Mock<ICashRegisterResolutionService>();
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());

        var sut = new CardPaymentService(
            ctx,
            gateway.Object,
            resolution.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            Options.Create(new PaymentGatewayOptions { RequireCardIntentForPosPayments = requireCardIntent }),
            http.Object,
            NullLogger<CardPaymentService>.Instance);

        return (sut, gateway, resolution, ctx);
    }

    private static void SetupRegisterOk(Mock<ICashRegisterResolutionService> resolution, Guid registerId) =>
        resolution.Setup(r => r.ValidatePaymentRegisterForCommitAsync(
                UserId, registerId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Success(registerId, "R-1"));

    private static PaymentIntentResult OkIntent(string intentId, PaymentIntentStatus status, string? clientSecret = null) =>
        new()
        {
            Success = true,
            PaymentIntentId = intentId,
            TransactionId = intentId,
            Status = status,
            ClientSecret = clientSecret
        };

    private static async Task<CardPaymentTransaction> SeedIntentAsync(
        AppDbContext ctx,
        string status,
        string gatewayIntentId = "pi_seed",
        decimal amount = 10m,
        Guid? paymentId = null)
    {
        var row = new CardPaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = Guid.NewGuid(),
            Amount = amount,
            Currency = "EUR",
            Gateway = "Mock",
            GatewayPaymentIntentId = gatewayIntentId,
            GatewayTransactionId = gatewayIntentId,
            Status = status,
            CreatedByUserId = UserId,
            PaymentId = paymentId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CardPaymentTransactions.Add(row);
        await ctx.SaveChangesAsync();
        return row;
    }
}
