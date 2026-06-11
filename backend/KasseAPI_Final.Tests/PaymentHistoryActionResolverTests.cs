using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentHistoryActionResolverTests
{
    private static PaymentHistoryService CreateActionSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PaymentHistoryActions_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        var reversalMock = new Mock<IPaymentReversalApprovalService>();
        return new PaymentHistoryService(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary),
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IUserService>(),
            reversalMock.Object,
            NullLogger<PaymentHistoryService>.Instance);
    }

    private static PaymentDetails SalePayment() => new()
    {
        Id = Guid.NewGuid(),
        CashRegisterId = Guid.NewGuid(),
        TotalAmount = 45.5m,
        ReceiptNumber = "AT-K1-20260611-1",
        IsActive = true,
        CreatedAt = DateTime.UtcNow.AddHours(-1),
        PaymentMethodRaw = "0",
    };

    private static PaymentHistoryActorContext CashierActor() =>
        new("u1", Roles.Cashier, CanCancel: true, CanRefund: true);

    private static PaymentHistoryActorContext ManagerActor() =>
        new("m1", Roles.Manager, CanCancel: true, CanRefund: true);

    [Fact]
    public void GetAvailableActions_Cashier_OffersStornoOnlyWithinWindow()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        var reversal = new PaymentHistoryReversalState(false, false, 0m);

        var actions = svc.GetAvailableActions(
            payment, reversal, CashierActor(), windowHours: 24, false, false, false);

        Assert.Contains(actions, a => a.Action == "storno");
        Assert.DoesNotContain(actions, a => a.Action == "refund");
    }

    [Fact]
    public void GetAvailableActions_Manager_OffersStornoAndRefund()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        var reversal = new PaymentHistoryReversalState(false, false, 0m);

        var actions = svc.GetAvailableActions(
            payment, reversal, ManagerActor(), windowHours: 24, false, false, false);

        Assert.Contains(actions, a => a.Action == "storno");
        Assert.Contains(actions, a => a.Action == "refund");
    }

    [Fact]
    public void GetAvailableActions_AfterStorno_OnlyViewOnly()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        var reversal = new PaymentHistoryReversalState(HasStornoChild: true, false, 0m);

        var actions = svc.GetAvailableActions(
            payment, reversal, ManagerActor(), 24, false, false, false);

        Assert.Single(actions);
        Assert.Equal("view_only", actions[0].Action);
        Assert.Equal("paymentHistory.actions.view", actions[0].LabelKey);
    }

    [Fact]
    public void GetAvailableActions_StornoRow_OnlyViewOnly()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        payment.IsStorno = true;

        var actions = svc.GetAvailableActions(
            payment,
            new PaymentHistoryReversalState(false, false, 0m),
            ManagerActor(),
            24,
            false,
            false,
            false);

        Assert.Single(actions);
        Assert.Equal("view_only", actions[0].Action);
    }

    [Fact]
    public void GetAvailableActions_VoucherSale_NoRefundAction()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        payment.PaymentMethodRaw = ((int)PaymentMethod.Voucher).ToString();

        var actions = svc.GetAvailableActions(
            payment,
            new PaymentHistoryReversalState(false, false, 0m),
            ManagerActor(),
            24,
            hasVoucherRedemption: true,
            false,
            false);

        Assert.Contains(actions, a => a.Action == "storno");
        Assert.DoesNotContain(actions, a => a.Action == "refund");
    }

    [Fact]
    public void GetAvailableActions_CashierStorno_RequiresManagerApproval()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();

        var storno = svc.GetAvailableActions(
            payment,
            new PaymentHistoryReversalState(false, false, 0m),
            CashierActor(),
            24,
            false,
            policyStornoApproval: false,
            false)
            .Single(a => a.Action == "storno");

        Assert.True(storno.RequiresManagerApproval);
        Assert.Equal("paymentHistory.reasons.stornoTitle", storno.ReasonLabelKey);
        Assert.Contains(storno.ReasonOptions, o => o.Code == "CUSTOMER_REQUEST");
        Assert.All(storno.ReasonOptions, o => Assert.StartsWith("paymentHistory.reasons.", o.LabelKey));
    }

    [Fact]
    public void GetAvailableActions_OutsideWindow_BlocksStornoButAllowsManagerRefund()
    {
        var svc = CreateActionSut();
        var payment = SalePayment();
        payment.CreatedAt = DateTime.UtcNow.AddHours(-30);

        var actions = svc.GetAvailableActions(
            payment,
            new PaymentHistoryReversalState(false, false, 0m),
            ManagerActor(),
            windowHours: 24,
            false,
            false,
            false);

        Assert.DoesNotContain(actions, a => a.Action == "storno");
        Assert.Contains(actions, a => a.Action == "refund");
    }
}
