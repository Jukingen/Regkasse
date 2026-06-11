using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosStornoControllerTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;
    private const string CashierId = "cashier-storno";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosStorno_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantId));
    }

    private static PosStornoController CreateController(
        AppDbContext ctx,
        Mock<IPaymentService> paymentMock,
        Mock<IAuditLogService>? auditMock = null)
    {
        auditMock ??= new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        var controller = new PosStornoController(
            ctx,
            paymentMock.Object,
            Mock.Of<IUserService>(),
            auditMock.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, CashierId),
            new(ClaimTypes.Role, Roles.Cashier),
            new(PermissionCatalog.PermissionClaimType, AppPermissions.PaymentCancel),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };
        return controller;
    }

    private static PaymentDetails SalePayment(Guid id, DateTime createdAt) => new()
    {
        Id = id,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Guest",
        TableNumber = 1,
        CashierId = CashierId,
        TotalAmount = 25m,
        TaxAmount = 2m,
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        TseSignature = "sig",
        TseTimestamp = createdAt,
        CashRegisterId = Guid.NewGuid(),
        ReceiptNumber = "R-100",
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        IsActive = true,
    };

    [Fact]
    public async Task StornoPayment_NotFound_ReturnsPaymentNotFoundKey()
    {
        await using var ctx = CreateContext();
        var paymentMock = new Mock<IPaymentService>();
        paymentMock.Setup(x => x.GetPaymentAsync(It.IsAny<Guid>())).ReturnsAsync((PaymentDetails?)null);

        var controller = CreateController(ctx, paymentMock);
        var result = await controller.StornoPayment(new StornoRequest
        {
            PaymentId = Guid.NewGuid(),
            Reason = "Customer changed mind",
            ReasonCode = "CUSTOMER_REQUEST",
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var body = Assert.IsType<StornoResponse>(notFound.Value);
        Assert.False(body.Success);
        Assert.Equal("errors.paymentNotFound", body.ErrorKey);
    }

    [Fact]
    public async Task StornoPayment_Outside24h_ReturnsTimeLimitError()
    {
        await using var ctx = CreateContext();
        var paymentId = Guid.NewGuid();
        var paymentMock = new Mock<IPaymentService>();
        paymentMock.Setup(x => x.GetPaymentAsync(paymentId))
            .ReturnsAsync(SalePayment(paymentId, DateTime.UtcNow.AddHours(-30)));

        var controller = CreateController(ctx, paymentMock);
        var result = await controller.StornoPayment(new StornoRequest
        {
            PaymentId = paymentId,
            Reason = "Customer changed mind",
            ReasonCode = "CUSTOMER_REQUEST",
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<StornoResponse>(bad.Value);
        Assert.Equal("errors.stornoTimeLimitExceeded", body.ErrorKey);
    }

    [Fact]
    public async Task StornoPayment_Success_ReturnsStornoPaymentId()
    {
        await using var ctx = CreateContext();
        var paymentId = Guid.NewGuid();
        var stornoId = Guid.NewGuid();
        var paymentMock = new Mock<IPaymentService>();
        paymentMock.Setup(x => x.GetPaymentAsync(paymentId))
            .ReturnsAsync(SalePayment(paymentId, DateTime.UtcNow.AddHours(-1)));
        paymentMock.Setup(x => x.CancelPaymentAsync(
                paymentId,
                It.IsAny<string>(),
                CashierId,
                It.IsAny<string?>(),
                CancellationReasonCode.CustomerRequest,
                It.IsAny<string?>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = true,
                PaymentId = stornoId,
                Payment = new PaymentDetails { Id = stornoId, IsStorno = true },
            });

        var controller = CreateController(ctx, paymentMock);
        var result = await controller.StornoPayment(new StornoRequest
        {
            PaymentId = paymentId,
            Reason = "Customer changed mind",
            ReasonCode = "CUSTOMER_REQUEST",
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<StornoResponse>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(stornoId, body.StornoPaymentId);
        Assert.Equal("messages.stornoSuccess", body.MessageKey);
    }
}
