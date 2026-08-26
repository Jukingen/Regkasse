using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for <see cref="PaymentGatewayService"/>. External gateways are mocked.
/// </summary>
public sealed class PaymentGatewayServiceTests
{
    private const string UserId = "cashier1";

    [Fact]
    public async Task ProcessPayment_WhenAmountInvalid_ReturnsInvalidAmount()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 0m,
                Method = "card",
                CashRegisterId = Guid.NewGuid()
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.InvalidAmount, result.ErrorCode);
        Assert.Null(result.Payment);
    }

    [Fact]
    public async Task ProcessPayment_WhenMethodInvalid_ReturnsInvalidMethod()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 10m,
                Method = "bank",
                CashRegisterId = Guid.NewGuid()
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.InvalidMethod, result.ErrorCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenRegisterDecommissioned_PropagatesCode()
    {
        var registerId = Guid.NewGuid();
        var (sut, _, resolution, _) = CreateSut();
        resolution.Setup(r => r.ValidatePaymentRegisterForCommitAsync(
                UserId, registerId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Decommissioned, "Kasse stillgelegt"));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 10m,
                Method = "card",
                CashRegisterId = registerId
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Decommissioned, result.ErrorCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenGatewayThrows_ReturnsGatewayError()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gateway down"));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 10m,
                Method = "paypal",
                CashRegisterId = registerId
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.GatewayError, result.ErrorCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenGatewayFails_ReturnsCreateFailed()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult { Success = false, ErrorMessage = "declined" });

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 10m,
                Method = "card",
                CashRegisterId = registerId
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.CreateFailed, result.ErrorCode);
        Assert.Equal("declined", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPayment_WhenGatewaySucceeds_PersistsAwaitingRow()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, ctx) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_create", PaymentIntentStatus.Pending, redirect: "https://pay.test/r"));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 12.50m,
                Currency = "eur",
                Method = "PayPal",
                CashRegisterId = registerId,
                IdempotencyKey = "idem-1",
                Description = "Tisch 1",
                ReturnUrl = "https://pos.example/return"
            },
            UserId);

        Assert.True(result.Ok);
        Assert.NotNull(result.Payment);
        Assert.Equal(12.50m, result.Payment!.Amount);
        Assert.Equal("EUR", result.Payment.Currency);
        Assert.Equal(OnlinePaymentStatuses.AwaitingPaymentGateway, result.Payment.Status);
        Assert.Equal("paypal", result.Payment.Method);
        Assert.Equal("https://pay.test/r", result.Payment.RedirectUrl);
        Assert.Equal("pi_create", result.Payment.PaymentIntentId);
        Assert.Null(result.Payment.PaymentDetailsId);
        Assert.Equal(1, await ctx.CardPaymentTransactions.CountAsync());
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task ProcessPayment_WhenReturnUrlOmitted_UsesNativeDeepLink()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        CreatePaymentIntentRequest? captured = null;
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreatePaymentIntentRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(OkIntent("pi_ret", PaymentIntentStatus.Pending));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 5m,
                Method = "card",
                CashRegisterId = registerId
            },
            UserId);

        Assert.True(result.Ok);
        Assert.Equal(PaymentReturnUrls.NativeDeepLink, captured!.ReturnUrl);
    }

    [Fact]
    public async Task ProcessPayment_WhenRelativeWebReturnUrl_IsAccepted()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_web", PaymentIntentStatus.Pending));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 5m,
                Method = "card",
                CashRegisterId = registerId,
                ReturnUrl = PaymentReturnUrls.WebPath
            },
            UserId);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKey_DoesNotCallGatewayTwice()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_idem", PaymentIntentStatus.Created));

        var request = new InitiateOnlinePaymentRequest
        {
            Amount = 5m,
            Method = "card",
            CashRegisterId = registerId,
            IdempotencyKey = "same-key"
        };

        var first = await sut.ProcessPaymentAsync(request, UserId);
        var second = await sut.ProcessPaymentAsync(request, UserId);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(first.Payment!.Id, second.Payment!.Id);
        gateway.Verify(
            g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPayment_WhenReturnUrlIsLoopbackOutsideDevelopment_Fails()
    {
        var registerId = Guid.NewGuid();
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var (sut, gateway, resolution, _) = CreateSut(environment: env.Object);
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_url", PaymentIntentStatus.Created));

        var result = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 4m,
                Method = "card",
                CashRegisterId = registerId,
                ReturnUrl = "http://localhost:8081/online-payment/callback"
            },
            UserId);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.InvalidReturnUrl, result.ErrorCode);
        gateway.Verify(
            g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.GetByIdAsync(Guid.NewGuid());
        Assert.False(result.Ok);
        Assert.True(result.IsNotFound);
        Assert.Equal(OnlinePaymentErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetById_CrossTenant_ReturnsNotFound()
    {
        var otherTenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, tenantId: otherTenant);
        await ctx.SaveChangesAsync();

        var result = await sut.GetByIdAsync(row.Id);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task VerifyWebhook_UnknownProvider_IsInvalid()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.VerifyWebhookAsync("unknown", "{}", new HeaderDictionary());
        Assert.False(result.SignatureValid);
        Assert.Equal(OnlinePaymentErrorCodes.UnknownProvider, result.ErrorCode);
    }

    [Fact]
    public async Task VerifyWebhook_MockSucceeded_TransitionsToCompleted_DoesNotCreatePaymentDetails()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, gatewayIntentId: "pi_wh");
        await ctx.SaveChangesAsync();

        var payload = """{"paymentIntentId":"pi_wh","status":"succeeded","eventId":"evt-1","transactionId":"txn-9"}""";
        var result = await sut.VerifyWebhookAsync("mock", payload, new HeaderDictionary());

        Assert.True(result.SignatureValid);
        Assert.True(result.Applied);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, result.Payment!.Status);
        Assert.Null(result.Payment.PaymentDetailsId);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.CardPaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == row.Id);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, stored.Status);
        Assert.Equal("evt-1", stored.LastWebhookEventId);
        Assert.Equal("txn-9", stored.GatewayTransactionId);
        Assert.Null(stored.PaymentId);
        Assert.NotNull(stored.CompletedAt);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task VerifyWebhook_DuplicateEventId_IsIdempotent()
    {
        var (sut, _, _, ctx) = CreateSut();
        SeedPayment(ctx, gatewayIntentId: "pi_dup");
        await ctx.SaveChangesAsync();

        var payload = """{"paymentIntentId":"pi_dup","status":"succeeded","eventId":"evt-dup"}""";
        var first = await sut.VerifyWebhookAsync("paypal", payload, new HeaderDictionary());
        var second = await sut.VerifyWebhookAsync("paypal", payload, new HeaderDictionary());

        Assert.True(first.Applied);
        Assert.True(second.SignatureValid);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, second.Payment!.Status);
        Assert.Equal(1, await ctx.CardPaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task VerifyWebhook_IllegalTransition_LeavesStatusUnchanged()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, gatewayIntentId: "pi_term", status: OnlinePaymentStatuses.GatewaySucceeded);
        await ctx.SaveChangesAsync();

        var payload = """{"paymentIntentId":"pi_term","status":"failed","eventId":"evt-late"}""";
        var result = await sut.VerifyWebhookAsync("mock", payload, new HeaderDictionary());

        Assert.True(result.SignatureValid);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, result.Payment!.Status);
        ctx.ChangeTracker.Clear();
        Assert.Equal(
            CardPaymentTransactionStatuses.Succeeded,
            (await ctx.CardPaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == row.Id)).Status);
    }

    [Fact]
    public async Task VerifyWebhook_WithSecret_RejectsBadHmac()
    {
        var (sut, _, _, _) = CreateSut(options: new PaymentGatewayOptions
        {
            StripeWebhookSecret = "whsec_test"
        });

        var headers = new HeaderDictionary { ["X-Payment-Webhook-Signature"] = "deadbeef" };
        var result = await sut.VerifyWebhookAsync("mock", """{"paymentIntentId":"pi_x","status":"succeeded"}""", headers);

        Assert.False(result.SignatureValid);
        Assert.Equal(OnlinePaymentErrorCodes.InvalidWebhook, result.ErrorCode);
    }

    [Fact]
    public async Task VerifyWebhook_WithSecret_AcceptsValidHmac()
    {
        var secret = "whsec_test";
        var (sut, _, _, ctx) = CreateSut(options: new PaymentGatewayOptions { StripeWebhookSecret = secret });
        SeedPayment(ctx, gatewayIntentId: "pi_hmac");
        await ctx.SaveChangesAsync();

        var payload = """{"paymentIntentId":"pi_hmac","status":"failed","eventId":"evt-h"}""";
        var headers = new HeaderDictionary
        {
            ["X-Payment-Webhook-Signature"] = SignHmac(secret, payload)
        };

        var result = await sut.VerifyWebhookAsync("mock", payload, headers);
        Assert.True(result.SignatureValid);
        Assert.Equal(OnlinePaymentStatuses.Failed, result.Payment!.Status);
    }

    [Fact]
    public async Task SimulateTestOutcome_Success_MarksCompleted()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx);
        await ctx.SaveChangesAsync();

        var result = await sut.SimulateTestOutcomeAsync(new SimulateOnlinePaymentRequest
        {
            OnlinePaymentId = row.Id,
            Outcome = "success"
        });

        Assert.True(result.Ok);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, result.Payment!.Status);
        Assert.Null(result.Payment.PaymentDetailsId);
    }

    [Fact]
    public async Task SimulateTestOutcome_Failed_FromSucceeded_IsIllegal()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, status: OnlinePaymentStatuses.GatewaySucceeded);
        await ctx.SaveChangesAsync();

        var result = await sut.SimulateTestOutcomeAsync(new SimulateOnlinePaymentRequest
        {
            OnlinePaymentId = row.Id,
            Outcome = "failed"
        });

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.IllegalTransition, result.ErrorCode);
    }

    [Fact]
    public async Task SimulateTestOutcome_SameStatus_IsIdempotent()
    {
        var (sut, _, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, status: OnlinePaymentStatuses.GatewaySucceeded);
        await ctx.SaveChangesAsync();

        var result = await sut.SimulateTestOutcomeAsync(new SimulateOnlinePaymentRequest
        {
            OnlinePaymentId = row.Id,
            Outcome = "completed"
        });

        Assert.True(result.Ok);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, result.Payment!.Status);
    }

    [Fact]
    public async Task CreateIntentAsync_IsAliasOfProcessPayment()
    {
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut();
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkIntent("pi_alias", PaymentIntentStatus.Pending));

        var result = await sut.CreateIntentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 8m,
                Method = "card",
                CashRegisterId = registerId
            },
            UserId);

        Assert.True(result.Ok);
        Assert.Equal("pi_alias", result.Payment!.PaymentIntentId);
    }

    [Fact]
    public async Task RefundAsync_WhenSucceeded_MarksRefunded()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = SeedPayment(ctx, status: OnlinePaymentStatuses.GatewaySucceeded, gatewayIntentId: "pi_ref");
        await ctx.SaveChangesAsync();
        gateway.Setup(g => g.RefundPaymentAsync("pi_ref", 10m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundResult { Success = true, RefundId = "re_1", RefundedAmount = 10m });
        gateway.Setup(g => g.RefundTransactionAsync("pi_ref", 10m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundResult { Success = true, RefundId = "re_1", RefundedAmount = 10m });

        var result = await sut.RefundAsync(row.Id, 10m);

        Assert.True(result.Ok);
        Assert.Equal(OnlinePaymentStatuses.Refunded, result.Payment!.Status);
        ctx.ChangeTracker.Clear();
        Assert.Equal(
            CardPaymentTransactionStatuses.Refunded,
            (await ctx.CardPaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == row.Id)).Status);
    }

    [Fact]
    public async Task RefundAsync_WhenPending_IsIllegal()
    {
        var (sut, gateway, _, ctx) = CreateSut();
        var row = SeedPayment(ctx);
        await ctx.SaveChangesAsync();

        var result = await sut.RefundAsync(row.Id, 10m);

        Assert.False(result.Ok);
        Assert.Equal(OnlinePaymentErrorCodes.IllegalTransition, result.ErrorCode);
        gateway.Verify(
            g => g.RefundTransactionAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(OnlinePaymentStatuses.Pending, OnlinePaymentStatuses.AwaitingPaymentGateway, true)]
    [InlineData(OnlinePaymentStatuses.Pending, OnlinePaymentStatuses.Completed, false)]
    [InlineData(OnlinePaymentStatuses.Pending, OnlinePaymentStatuses.Failed, true)]
    [InlineData(OnlinePaymentStatuses.AwaitingPaymentGateway, OnlinePaymentStatuses.Completed, false)]
    [InlineData(OnlinePaymentStatuses.AwaitingPaymentGateway, OnlinePaymentStatuses.Failed, true)]
    [InlineData(OnlinePaymentStatuses.Completed, OnlinePaymentStatuses.Refunded, true)]
    [InlineData(OnlinePaymentStatuses.GatewaySucceeded, OnlinePaymentStatuses.Refunded, true)]
    [InlineData(OnlinePaymentStatuses.Completed, OnlinePaymentStatuses.Failed, false)]
    [InlineData(OnlinePaymentStatuses.Failed, OnlinePaymentStatuses.Completed, false)]
    [InlineData(OnlinePaymentStatuses.Refunded, OnlinePaymentStatuses.Completed, false)]
    [InlineData(OnlinePaymentStatuses.AwaitingPaymentGateway, OnlinePaymentStatuses.Pending, false)]
    [InlineData(OnlinePaymentStatuses.Pending, OnlinePaymentStatuses.GatewaySucceeded, true)]
    public void StateMachine_CanTransition(string from, string to, bool allowed)
    {
        Assert.Equal(allowed, OnlinePaymentStatuses.CanTransition(from, to));
    }

    private static (PaymentGatewayService Sut, Mock<IPaymentGateway> Gateway, Mock<ICashRegisterResolutionService> Resolution, AppDbContext Ctx)
        CreateSut(PaymentGatewayOptions? options = null, IHostEnvironment? environment = null)
    {
        var ctx = PaymentServiceCoverageHarness.CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        ctx.SaveChanges();

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(g => g.ProviderName).Returns("Mock");

        var resolution = new Mock<ICashRegisterResolutionService>();
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());

        var sut = new PaymentGatewayService(
            ctx,
            gateway.Object,
            resolution.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            Options.Create(options ?? new PaymentGatewayOptions()),
            http.Object,
            NullLogger<PaymentGatewayService>.Instance,
            environment);

        return (sut, gateway, resolution, ctx);
    }

    private static void SetupRegisterOk(Mock<ICashRegisterResolutionService> resolution, Guid registerId) =>
        resolution.Setup(r => r.ValidatePaymentRegisterForCommitAsync(
                UserId, registerId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Success(registerId, "R-1"));

    private static PaymentIntentResult OkIntent(string intentId, PaymentIntentStatus status, string? redirect = null) =>
        new()
        {
            Success = true,
            PaymentIntentId = intentId,
            TransactionId = intentId,
            Status = status,
            ClientSecret = "secret",
            RedirectUrl = redirect
        };

    private static CardPaymentTransaction SeedPayment(
        AppDbContext ctx,
        string status = OnlinePaymentStatuses.AwaitingPaymentGateway,
        string gatewayIntentId = "pi_seed",
        Guid? tenantId = null)
    {
        var row = new CardPaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? SystemTenantIds.Platform,
            CashRegisterId = Guid.NewGuid(),
            Amount = 10m,
            Currency = "EUR",
            Gateway = "Mock",
            MethodCode = OnlinePaymentMethods.Card,
            GatewayPaymentIntentId = gatewayIntentId,
            GatewayTransactionId = gatewayIntentId,
            Status = OnlinePaymentStatuses.ToCardStatus(status),
            CreatedByUserId = UserId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CardPaymentTransactions.Add(row);
        return row;
    }

    private static string SignHmac(string secret, string payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
