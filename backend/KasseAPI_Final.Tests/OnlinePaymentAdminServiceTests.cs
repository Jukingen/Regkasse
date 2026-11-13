using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.OnlinePayments;
using KasseAPI_Final.Services.PaymentGateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlinePaymentAdminServiceTests
{
    [Fact]
    public async Task CreateTestPayment_persistsGatewayIntent()
    {
        var (sut, db, tenantId, _) = CreateSut();

        var result = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.Create,
            Amount = 10.00m,
            PaymentMethod = "card"
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Transaction);
        Assert.True(result.Transaction!.IsSynthetic);
        Assert.Equal(10.00m, result.Transaction.Amount);
        Assert.Equal("card", result.Transaction.PaymentMethod);
        Assert.Equal(CardPaymentTransactionStatuses.Pending, result.Transaction.Status);
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.CardPaymentTransactions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(tenantId, result.Transaction.TenantId);
    }

    [Fact]
    public async Task CreateTestPayment_rejectsInvalidMethod()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.Create,
            Amount = 5m,
            PaymentMethod = "bitcoin"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(OnlinePaymentAdminService.ValidationCode, result.Code);
    }

    [Fact]
    public async Task SimulateWebhookSuccess_marksGatewaySucceeded()
    {
        var (sut, db, _, _) = CreateSut();
        var created = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.Create,
            Amount = 5m,
            PaymentMethod = "paypal"
        });
        Assert.True(created.Succeeded);

        var webhook = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.WebhookSucceeded,
            TransactionId = created.Transaction!.Id
        });

        Assert.True(webhook.Succeeded);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, webhook.Transaction!.Status);
        Assert.Equal(OnlinePaymentAdminService.WebhookSucceededEvent, webhook.Transaction.LastWebhookEvent);

        db.ChangeTracker.Clear();
        var stored = await db.CardPaymentTransactions.IgnoreQueryFilters()
            .SingleAsync(x => x.Id == created.Transaction.Id);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, stored.Status);
        Assert.Null(stored.PaymentId);
    }

    [Fact]
    public async Task SimulateWebhookFailed_marksFailed()
    {
        var (sut, _, _, _) = CreateSut();
        var created = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.Create,
            Amount = 12.34m,
            PaymentMethod = "card"
        });

        var webhook = await sut.RunTestAsync(new AdminOnlinePaymentTestRequest
        {
            Action = OnlinePaymentTestActions.WebhookFailed,
            TransactionId = created.Transaction!.Id
        });

        Assert.True(webhook.Succeeded);
        Assert.Equal(CardPaymentTransactionStatuses.Failed, webhook.Transaction!.Status);
        Assert.Equal(OnlinePaymentAdminService.WebhookFailedEvent, webhook.Transaction.LastWebhookEvent);
    }

    [Fact]
    public async Task List_includesPersistedGatewayIntents()
    {
        var (sut, db, tenantId, _) = CreateSut();
        var paymentId = Guid.NewGuid();
        db.CardPaymentTransactions.Add(new CardPaymentTransaction
        {
            Id = paymentId,
            TenantId = tenantId,
            CashRegisterId = Guid.NewGuid(),
            Amount = 12m,
            Currency = "EUR",
            Gateway = "Mock",
            MethodCode = OnlinePaymentMethods.Card,
            Status = CardPaymentTransactionStatuses.Succeeded,
            GatewayPaymentIntentId = "pi_live_1",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var list = await sut.ListAsync(1, 50);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(paymentId, list.Items[0].Id);
        Assert.Equal(CardPaymentTransactionStatuses.Succeeded, list.Items[0].Status);
        Assert.False(list.Items[0].IsSynthetic);
        Assert.Equal("Pay Cafe", list.Items[0].TenantName);
    }

    private static (
        OnlinePaymentAdminService Sut,
        AppDbContext Db,
        Guid TenantId,
        Mock<IPaymentGateway> Gateway)
        CreateSut()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pay Cafe",
            Slug = "pay-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "K-OP-1",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            IsDefaultForTenant = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var factory = TenantTestDoubles.DbContextFactoryForTests(options, accessor);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(g => g.ProviderName).Returns("Mock");
        gateway
            .Setup(g => g.CreatePaymentIntentAsync(
                It.IsAny<CreatePaymentIntentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult
            {
                Success = true,
                PaymentIntentId = "pi_admin_test",
                TransactionId = "pi_admin_test",
                Status = PaymentIntentStatus.Pending,
                ClientSecret = "secret"
            });

        var sut = new OnlinePaymentAdminService(
            factory,
            accessor,
            gateway.Object,
            TimeProvider.System,
            NullLogger<OnlinePaymentAdminService>.Instance);

        return (sut, db, tenantId, gateway);
    }
}
