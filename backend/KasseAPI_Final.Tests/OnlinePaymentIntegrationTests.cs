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
/// PostgreSQL proofs for online-payment state transitions and unique idempotency.
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class OnlinePaymentIntegrationTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public OnlinePaymentIntegrationTests(PostgreSqlReplayFixture fixture) =>
        _fixture = fixture;

    [SkippableFact]
    public async Task ProcessPayment_ThenWebhook_PersistsCompletedState()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var (sut, gateway, resolution, _) = CreateSut(tenantId);
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult
            {
                Success = true,
                PaymentIntentId = $"pi-{Guid.NewGuid():N}",
                Status = PaymentIntentStatus.Pending,
                RedirectUrl = "https://mock-pay.local/checkout"
            });

        var initiated = await sut.ProcessPaymentAsync(
            new InitiateOnlinePaymentRequest
            {
                Amount = 7.5m,
                Method = "card",
                CashRegisterId = registerId,
                IdempotencyKey = $"idem-{Guid.NewGuid():N}"[..32]
            },
            "cashier1");

        Assert.True(initiated.Ok);
        Assert.Equal(OnlinePaymentStatuses.AwaitingPaymentGateway, initiated.Payment!.Status);

        var payload =
            $"{{\"paymentIntentId\":\"{initiated.Payment.PaymentIntentId}\",\"status\":\"succeeded\",\"eventId\":\"evt-{Guid.NewGuid():N}\"}}";
        var webhook = await sut.VerifyWebhookAsync("mock", payload, new HeaderDictionary());

        Assert.True(webhook.Applied);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, webhook.Payment!.Status);
        Assert.Null(webhook.Payment.PaymentDetailsId);

        await using var verify = CreateContext(tenantId);
        var stored = await verify.CardPaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == initiated.Payment.Id);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, stored.Status);
        Assert.Null(stored.PaymentId);
        Assert.NotNull(stored.CompletedAt);
        Assert.Equal(0, await verify.PaymentDetails.CountAsync());
    }

    [SkippableFact]
    public async Task ProcessPayment_SameIdempotencyKey_ReturnsExistingRow()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var key = $"k-{Guid.NewGuid():N}"[..24];
        var (sut, gateway, resolution, _) = CreateSut(tenantId);
        SetupRegisterOk(resolution, registerId);
        gateway.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PaymentIntentResult
            {
                Success = true,
                PaymentIntentId = $"pi-{Guid.NewGuid():N}",
                Status = PaymentIntentStatus.Created
            });

        var request = new InitiateOnlinePaymentRequest
        {
            Amount = 3m,
            Method = "paypal",
            CashRegisterId = registerId,
            IdempotencyKey = key
        };

        var first = await sut.ProcessPaymentAsync(request, "cashier1");
        var second = await sut.ProcessPaymentAsync(request, "cashier1");

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(first.Payment!.Id, second.Payment!.Id);
        gateway.Verify(
            g => g.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await using var verify = CreateContext(tenantId);
        Assert.Equal(1, await verify.CardPaymentTransactions.CountAsync(p => p.IdempotencyKey == key));
    }

    [SkippableFact]
    public async Task UniqueIdempotencyIndex_RejectsDuplicateInsert()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var tenantId = Guid.NewGuid();
        var key = $"u-{Guid.NewGuid():N}"[..24];
        await using (var seed = CreateContext(tenantId))
        {
            TenantTestDoubles.EnsureTenant(seed, tenantId);
            seed.CardPaymentTransactions.Add(NewRow(tenantId, key, $"pi-{Guid.NewGuid():N}"));
            await seed.SaveChangesAsync();
        }

        await using var ctx = CreateContext(tenantId);
        ctx.CardPaymentTransactions.Add(NewRow(tenantId, key, $"pi-{Guid.NewGuid():N}"));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task SucceededToRefunded_IsAllowed_SucceededToFailed_IsNot()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        var row = NewRow(tenantId, null, $"pi-{Guid.NewGuid():N}");
        row.Status = CardPaymentTransactionStatuses.Succeeded;
        ctx.CardPaymentTransactions.Add(row);
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx, tenantId, new Mock<IPaymentGateway>(), new Mock<ICashRegisterResolutionService>());

        var refunded = await sut.VerifyWebhookAsync(
            "mock",
            $"{{\"paymentIntentId\":\"{row.GatewayPaymentIntentId}\",\"status\":\"refunded\",\"eventId\":\"evt-r\"}}",
            new HeaderDictionary());
        Assert.Equal(OnlinePaymentStatuses.Refunded, refunded.Payment!.Status);

        var failed = await sut.VerifyWebhookAsync(
            "mock",
            $"{{\"paymentIntentId\":\"{row.GatewayPaymentIntentId}\",\"status\":\"failed\",\"eventId\":\"evt-f\"}}",
            new HeaderDictionary());
        Assert.Equal(OnlinePaymentStatuses.Refunded, failed.Payment!.Status);
    }

    private AppDbContext CreateContext(Guid tenantId) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

    private (PaymentGatewayService Sut, Mock<IPaymentGateway> Gateway, Mock<ICashRegisterResolutionService> Resolution, AppDbContext Ctx)
        CreateSut(Guid tenantId)
    {
        var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        ctx.SaveChanges();

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(g => g.ProviderName).Returns("Mock");
        var resolution = new Mock<ICashRegisterResolutionService>();
        return (CreateService(ctx, tenantId, gateway, resolution), gateway, resolution, ctx);
    }

    private static PaymentGatewayService CreateService(
        AppDbContext ctx,
        Guid tenantId,
        Mock<IPaymentGateway> gateway,
        Mock<ICashRegisterResolutionService> resolution)
    {
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());
        return new PaymentGatewayService(
            ctx,
            gateway.Object,
            resolution.Object,
            TenantTestDoubles.SettingsResolverReturning(tenantId),
            Options.Create(new KasseAPI_Final.Configuration.PaymentGatewayOptions()),
            http.Object,
            NullLogger<PaymentGatewayService>.Instance);
    }

    private static void SetupRegisterOk(Mock<ICashRegisterResolutionService> resolution, Guid registerId) =>
        resolution.Setup(r => r.ValidatePaymentRegisterForCommitAsync(
                It.IsAny<string>(), registerId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterResolutionValidationResult.Success(registerId, "PG-OP"));

    private static CardPaymentTransaction NewRow(Guid tenantId, string? idempotencyKey, string intentId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = Guid.NewGuid(),
            Amount = 1m,
            Currency = "EUR",
            Gateway = "Mock",
            MethodCode = OnlinePaymentMethods.Card,
            GatewayPaymentIntentId = intentId,
            Status = CardPaymentTransactionStatuses.Pending,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
}
