using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentStornoRefundGateTests
{
    [Fact]
    public async Task Redirect_SaleRequest_ReturnsNullWithoutCallingReversalServices()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var storno = new Mock<IPaymentStornoService>();
        var refund = new Mock<IPaymentRefundService>();
        var sut = Create(ctx, storno.Object, refund.Object);
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", new Customer { Name = "Ada" });

        Assert.Null(result);
        storno.Verify(service => service.FindOriginalSalePaymentByReceiptNumberAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        refund.Verify(service => service.RefundPaymentAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<RefundReasonCode>(), It.IsAny<string?>()), Times.Never);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Redirect_BothFlags_RejectsBeforeRegisterLookup()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registers = new Mock<ICashRegisterResolutionService>();
        var sut = Create(ctx, registers: registers);
        var request = Request(isStorno: true, isRefund: true);

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", Customer(request));

        Assert.False(result!.Success);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal("Storno and refund are mutually exclusive", result.Message);
        registers.Verify(service => service.ValidatePaymentRegisterAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Redirect_EmptyRegister_Rejects()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);
        var request = Request(isStorno: true);
        request.CashRegisterId = Guid.Empty;

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", Customer(request));

        Assert.Equal(CashRegisterResolutionCodes.Required, result!.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
    }

    [Fact]
    public async Task Redirect_DecommissionedRegister_MapsRksvCode()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx, register: CashRegisterResolutionValidationResult.Failure(
            CashRegisterResolutionCodes.Decommissioned, "register closed"));
        var request = Request(isStorno: true);

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", Customer(request));

        Assert.False(result!.Success);
        Assert.Equal("register closed", result.Message);
        Assert.Equal(RksvGuardErrorCodes.RegisterDecommissioned, result.DiagnosticCode);
    }

    [Fact]
    public async Task Redirect_MissingReceiptOrOtherTenantOrWrongCustomer_Rejects()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var original = Original(Guid.NewGuid(), 12m);
        var missing = Create(ctx, storno: StornoReturning(null));
        var foreign = Create(ctx, storno: StornoReturning(original), belongs: _ => false);
        var mismatch = Create(ctx, storno: StornoReturning(original));
        var wrongCustomer = Request(isStorno: true);
        wrongCustomer.CustomerId = Guid.NewGuid();

        var missingResult = await missing.TryRedirectStornoOrRefundAsync(Request(isStorno: true), "cashier", Customer(Request(isStorno: true)));
        var foreignResult = await foreign.TryRedirectStornoOrRefundAsync(Request(isStorno: true, customerId: original.CustomerId), "cashier", new Customer { Name = "Ada" });
        var mismatchResult = await mismatch.TryRedirectStornoOrRefundAsync(wrongCustomer, "cashier", Customer(wrongCustomer));

        Assert.Equal("Original receipt not found", missingResult!.Message);
        Assert.Equal("Original receipt not found", foreignResult!.Message);
        Assert.Equal("Customer does not match original receipt", mismatchResult!.Message);
        Assert.True(missingResult.IsDeterministicFailure);
        Assert.True(foreignResult.IsDeterministicFailure);
        Assert.True(mismatchResult.IsDeterministicFailure);
    }

    [Fact]
    public async Task Redirect_StornoAmountMismatchAndExistingRefund_RejectWithoutSaving()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var original = Original(Guid.NewGuid(), 12m);
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            OriginalPaymentId = original.Id,
            IsRefund = true,
            IsActive = true,
            TotalAmount = 1m
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var storno = new Mock<IPaymentStornoService>();
        storno.Setup(service => service.FindOriginalSalePaymentByReceiptNumberAsync(It.IsAny<Guid>(), "R-1"))
            .ReturnsAsync(original);
        var mismatch = Create(ctx, storno.Object);
        var blocked = Create(ctx, storno.Object);
        var mismatchRequest = Request(isStorno: true, customerId: original.CustomerId, total: 10m);
        var blockedRequest = Request(isStorno: true, customerId: original.CustomerId, total: 12m);

        var mismatchResult = await mismatch.TryRedirectStornoOrRefundAsync(mismatchRequest, "cashier", Customer(mismatchRequest));
        var blockedResult = await blocked.TryRedirectStornoOrRefundAsync(blockedRequest, "cashier", Customer(blockedRequest));

        Assert.Equal("Storno total does not match original receipt", mismatchResult!.Message);
        Assert.Equal("STORNO_BLOCKED_BY_REFUNDS", blockedResult!.DiagnosticCode);
        storno.Verify(service => service.CreateStornoReversalAsync(
            It.IsAny<PaymentDetails>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<StornoReason?>()), Times.Never);
        Assert.Equal(1, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        await AssertNoWrite(ctx, expectedPayments: 1);
    }

    [Fact]
    public async Task Redirect_FullStorno_DelegatesWithTrimmedReceiptAndReason()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var resolvedRegisterId = Guid.NewGuid();
        var original = Original(Guid.NewGuid(), 12m);
        var storno = new Mock<IPaymentStornoService>();
        storno.Setup(service => service.FindOriginalSalePaymentByReceiptNumberAsync(resolvedRegisterId, "R-1"))
            .ReturnsAsync(original);
        storno.Setup(service => service.FormatStornoReasonForAudit(StornoReason.KundeStorniert)).Returns("Kunde storniert");
        storno.Setup(service => service.CreateStornoReversalAsync(
                original, "Kunde storniert", "cashier", "idem-1", StornoReason.KundeStorniert))
            .ReturnsAsync(new PaymentResult { Success = true, Message = "storno-ok" });
        var sut = Create(ctx, storno.Object, register: CashRegisterResolutionValidationResult.Success(resolvedRegisterId, "K1"));
        var request = Request(isStorno: true, customerId: original.CustomerId, total: 12.004m);
        request.OriginalReceiptNumber = " R-1 ";
        request.IdempotencyKey = " idem-1 ";
        request.StornoReason = StornoReason.KundeStorniert;

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", Customer(request));

        Assert.True(result!.Success);
        Assert.Equal("storno-ok", result.Message);
        storno.VerifyAll();
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Redirect_Refund_DelegatesRoundedAmountAndDefaultNotes()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var original = Original(Guid.NewGuid(), 20m);
        var refund = new Mock<IPaymentRefundService>();
        refund.Setup(service => service.RefundPaymentAsync(
                original.Id, 10.13m, "Refund (POS create)", "cashier", "idem-2", RefundReasonCode.Other, null))
            .ReturnsAsync(new PaymentResult { Success = true, Message = "refund-ok" });
        var sut = Create(ctx, refund: refund.Object, storno: StornoReturning(original));
        var request = Request(isRefund: true, customerId: original.CustomerId, total: 10.126m);
        request.IdempotencyKey = " idem-2 ";
        request.Notes = "   ";

        var result = await sut.TryRedirectStornoOrRefundAsync(request, "cashier", Customer(request));

        Assert.True(result!.Success);
        Assert.Equal("refund-ok", result.Message);
        refund.VerifyAll();
        await AssertNoWrite(ctx);
    }

    private static PaymentDetails Original(Guid customerId, decimal total) => new()
    {
        CustomerId = customerId,
        TotalAmount = total,
        CashRegisterId = Guid.NewGuid(),
        ReceiptNumber = "R-1"
    };

    private static CreatePaymentRequest Request(
        bool isStorno = false,
        bool isRefund = false,
        Guid? customerId = null,
        decimal total = 12m)
    {
        var id = customerId ?? Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(id, Guid.NewGuid(), Guid.NewGuid(), total: total);
        request.IsStorno = isStorno;
        request.IsRefund = isRefund;
        request.OriginalReceiptNumber = "R-1";
        request.StornoReason = StornoReason.FalscherBetrag;
        return request;
    }

    private static Customer Customer(CreatePaymentRequest request) => new()
    {
        Id = request.CustomerId,
        Name = "Ada"
    };

    private static IPaymentStornoService StornoReturning(PaymentDetails? original)
    {
        var storno = new Mock<IPaymentStornoService>();
        storno.Setup(service => service.FindOriginalSalePaymentByReceiptNumberAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(original);
        return storno.Object;
    }

    private static PaymentStornoRefundGate Create(
        AppDbContext context,
        IPaymentStornoService? storno = null,
        IPaymentRefundService? refund = null,
        CashRegisterResolutionValidationResult? register = null,
        Mock<ICashRegisterResolutionService>? registers = null,
        Func<PaymentDetails, bool>? belongs = null)
    {
        registers ??= new Mock<ICashRegisterResolutionService>();
        registers.Setup(service => service.ValidatePaymentRegisterAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(register ?? CashRegisterResolutionValidationResult.Success(Guid.NewGuid(), "K1"));
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());
        return new PaymentStornoRefundGate(
            context,
            NullLogger<PaymentService>.Instance,
            registers.Object,
            http.Object,
            storno ?? new Mock<IPaymentStornoService>().Object,
            refund ?? new Mock<IPaymentRefundService>().Object,
            (payment, _) => Task.FromResult(belongs?.Invoke(payment) ?? true));
    }

    private static async Task AssertNoWrite(AppDbContext context, int expectedPayments = 0)
    {
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(expectedPayments, await context.PaymentDetails.AsNoTracking().CountAsync());
    }
}
