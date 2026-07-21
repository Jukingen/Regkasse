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

public sealed class PosPaymentHistoryServiceTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;
    private const string CashierId = "cashier-history";

    private static (AppDbContext Context, IDbContextFactory<AppDbContext> Factory) CreateContextWithFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PaymentHistorySvc_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(TenantId);
        return (
            new AppDbContext(options, tenantAccessor),
            TenantTestDoubles.DbContextFactoryForTests(options, tenantAccessor));
    }

    private static PaymentHistoryService CreateService(
        AppDbContext ctx,
        Mock<IPaymentReversalApprovalService>? reversalMock = null)
    {
        reversalMock ??= new Mock<IPaymentReversalApprovalService>();
        reversalMock.Setup(x => x.AssessPolicyAsync(
                It.IsAny<PaymentDetails>(),
                It.IsAny<PaymentReversalOperation>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentReversalPolicyDto { RequiresApproval = false });

        return new PaymentHistoryService(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(TenantId),
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IUserService>(),
            reversalMock.Object,
            NullLogger<PaymentHistoryService>.Instance);
    }

    private static PaymentDetails MinimalPayment(
        Guid id,
        Guid registerId,
        DateTime createdAt,
        string receiptNumber,
        decimal amount = 10m) => new()
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 5,
            CashierId = CashierId,
            TotalAmount = amount,
            TaxAmount = 1m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = createdAt,
            CashRegisterId = registerId,
            ReceiptNumber = receiptNumber,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsActive = true,
        };

    [Fact]
    public async Task GetRecentPayments_NoRegister_ReturnsError()
    {
        var (ctx, _) = CreateContextWithFactory();
        await using (ctx)
        {
            var svc = CreateService(ctx);

            var (response, code, _) = await svc.GetRecentPaymentsAsync(
                new PaymentHistoryActorContext(CashierId, "Cashier", true, true),
                cashRegisterId: null);

            Assert.Null(response);
            Assert.Equal("POS_PAYMENT_HISTORY_NO_REGISTER", code);
        }
    }

    [Fact]
    public async Task GetRecentPayments_ReturnsRecentPaymentsForRegister()
    {
        var (ctx, factory) = CreateContextWithFactory();
        await using (ctx)
        {
            var registerId = Guid.NewGuid();
            ctx.CashRegisters.Add(new CashRegister
            {
                TenantId = TenantId,
                Id = registerId,
                RegisterNumber = "K1",
                Location = "Front",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });

            var recent = DateTime.UtcNow.AddHours(-2);
            var old = DateTime.UtcNow.AddHours(-30);
            ctx.PaymentDetails.Add(MinimalPayment(Guid.NewGuid(), registerId, recent, "R-RECENT", 45.5m));
            ctx.PaymentDetails.Add(MinimalPayment(Guid.NewGuid(), registerId, old, "R-OLD"));
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var (response, code, _) = await svc.GetRecentPaymentsAsync(
                new PaymentHistoryActorContext(CashierId, "Manager", true, true),
                registerId,
                hours: 24);

            Assert.Null(code);
            Assert.NotNull(response);
            Assert.Equal(1, response!.TotalCount);
            Assert.Single(response.Payments);
            Assert.Equal("R-RECENT", response.Payments[0].ReceiptNumber);
            Assert.Equal("de", response.Language);
            Assert.Contains(response.Payments[0].AvailableActions, a => a.Action == "storno");
        }
    }

    [Fact]
    public async Task GetRecentPayments_ResolvesRegisterFromActiveShift()
    {
        var (ctx, _) = CreateContextWithFactory();
        await using (ctx)
        {
            var registerId = Guid.NewGuid();
            ctx.CashRegisters.Add(new CashRegister
            {
                TenantId = TenantId,
                Id = registerId,
                RegisterNumber = "K2",
                Location = "Front",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            ctx.CashierShifts.Add(new CashierShift
            {
                TenantId = TenantId,
                CashRegisterId = registerId,
                CashierId = CashierId,
                CashierName = "Test",
                StartBalance = 100m,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                Status = CashierShiftStatuses.Active,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            ctx.PaymentDetails.Add(MinimalPayment(
                Guid.NewGuid(),
                registerId,
                DateTime.UtcNow.AddMinutes(-30),
                "R-SHIFT"));
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var (response, code, _) = await svc.GetRecentPaymentsAsync(
                new PaymentHistoryActorContext(CashierId, "Cashier", true, true),
                cashRegisterId: null);

            Assert.Null(code);
            Assert.NotNull(response);
            Assert.Equal(registerId, response!.CashRegisterId);
            Assert.Single(response.Payments);
        }
    }

    [Fact]
    public async Task GetRecentPayments_CashierRole_OmitsRefundAction()
    {
        var (ctx, factory) = CreateContextWithFactory();
        await using (ctx)
        {
            var registerId = Guid.NewGuid();
            ctx.CashRegisters.Add(new CashRegister
            {
                TenantId = TenantId,
                Id = registerId,
                RegisterNumber = "K3",
                Location = "Front",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            ctx.PaymentDetails.Add(MinimalPayment(
                Guid.NewGuid(),
                registerId,
                DateTime.UtcNow.AddMinutes(-10),
                "R-NO-REFUND"));
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var (response, _, _) = await svc.GetRecentPaymentsAsync(
                new PaymentHistoryActorContext(CashierId, "Cashier", CanCancel: true, CanRefund: true),
                registerId);

            var actions = response!.Payments[0].AvailableActions.Select(a => a.Action).ToList();
            Assert.Contains("storno", actions);
            Assert.DoesNotContain("refund", actions);
        }
    }
}
