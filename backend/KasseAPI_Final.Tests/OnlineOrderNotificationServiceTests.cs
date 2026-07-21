using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Order;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderNotificationServiceTests
{
    [Fact]
    public async Task SendOrderConfirmationAsync_sends_email_push_and_pos_activity()
    {
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IOnlineOrderCustomerEmailService>();
        email.Setup(e => e.TrySendOrderConfirmationAsync(
                It.IsAny<OnlineOrderCustomerEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var push = new Mock<IOnlineOrderPushSender>();
        push.Setup(p => p.TrySendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new OnlineOrderNotificationService(
            activity.Object,
            email.Object,
            push.Object,
            NullLogger<OnlineOrderNotificationService>.Instance);

        var tenantId = Guid.NewGuid();
        var order = new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-N1",
            CustomerName = "Max",
            CustomerEmail = "max@example.com",
            CustomerDeviceToken = "device-token-abc",
            CustomerPhone = "123",
            OrderType = OnlineOrderTypes.Takeaway,
            Total = 15.50m,
            OrderStatus = OnlineOrderStatuses.Accepted,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            PaymentStatus = OnlineOrderPaymentStatuses.Paid,
            Source = OnlineOrderSources.Native,
            PosCartId = "cart-1"
        };

        await sut.SendOrderConfirmationAsync(order, actorUserId: "user-1");

        email.Verify(
            e => e.TrySendOrderConfirmationAsync(
                It.Is<OnlineOrderCustomerEmailRequest>(r =>
                    r.ToEmail == "max@example.com"
                    && r.OrderNumber == "ORD-N1"
                    && r.EstimatedMinutes == OnlineOrderNotificationService.DefaultEstimatedMinutes),
                It.IsAny<CancellationToken>()),
            Times.Once);

        push.Verify(
            p => p.TrySendAsync(
                "device-token-abc",
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains("ORD-N1")),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OnlineOrderPushedToPos,
                It.IsAny<object?>(),
                "user-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_without_cart_publishes_confirmed()
    {
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new OnlineOrderNotificationService(
            activity.Object,
            Mock.Of<IOnlineOrderCustomerEmailService>(),
            Mock.Of<IOnlineOrderPushSender>(),
            NullLogger<OnlineOrderNotificationService>.Instance);

        var order = new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OrderNumber = "ORD-N2",
            CustomerName = "Anna",
            CustomerPhone = "1",
            OrderType = OnlineOrderTypes.Delivery,
            Total = 9m,
            OrderStatus = OnlineOrderStatuses.Accepted,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            Source = OnlineOrderSources.Web
        };

        await sut.SendOrderConfirmationAsync(order);

        activity.Verify(
            a => a.TryPublishAsync(
                order.TenantId,
                ActivityEventType.OnlineOrderConfirmed,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyStatusChanged_ready_sends_customer_channels()
    {
        var email = new Mock<IOnlineOrderCustomerEmailService>();
        email.Setup(e => e.TrySendOrderStatusAsync(
                It.IsAny<OnlineOrderCustomerEmailRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var push = new Mock<IOnlineOrderPushSender>();
        push.Setup(p => p.TrySendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new OnlineOrderNotificationService(
            activity.Object,
            email.Object,
            push.Object,
            NullLogger<OnlineOrderNotificationService>.Instance);

        var order = new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OrderNumber = "ORD-R1",
            CustomerName = "Max",
            CustomerEmail = "max@example.com",
            CustomerDeviceToken = "tok",
            CustomerPhone = "1",
            OrderType = OnlineOrderTypes.Takeaway,
            Total = 5m,
            OrderStatus = OnlineOrderStatuses.Ready,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            PaymentStatus = OnlineOrderPaymentStatuses.Paid,
            Source = OnlineOrderSources.Pwa
        };

        await sut.NotifyStatusChangedAsync(order, OnlineOrderStatuses.Preparing);

        email.Verify(
            e => e.TrySendOrderStatusAsync(
                It.IsAny<OnlineOrderCustomerEmailRequest>(),
                It.Is<string>(h => h.Contains("bereit", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        push.Verify(
            p => p.TrySendAsync(
                "tok",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
