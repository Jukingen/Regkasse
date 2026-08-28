using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosReceiptsControllerTests
{
    [Fact]
    public async Task GetRecent_WithoutCashRegisterId_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IReceiptService>(), Mock.Of<IPaymentService>());
        var result = await controller.GetRecent(Guid.Empty);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetReceipt_WrongRegister_ReturnsNotFound()
    {
        var receiptId = Guid.NewGuid();
        var registerA = Guid.NewGuid();
        var registerB = Guid.NewGuid();
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerA))
            .ReturnsAsync((ReceiptDTO?)null);

        var controller = CreateController(receipts.Object, Mock.Of<IPaymentService>());
        var result = await controller.GetReceipt(receiptId, registerA);
        Assert.IsType<NotFoundObjectResult>(result.Result);

        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerB))
            .ReturnsAsync(new ReceiptDTO { ReceiptId = receiptId, CashRegisterId = registerB });
        var ok = await controller.GetReceipt(receiptId, registerB);
        Assert.IsType<OkObjectResult>(ok.Result);
    }

    [Fact]
    public async Task Reprint_WrongRegister_ReturnsNotFound_AndDoesNotConfirm()
    {
        var receiptId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerId))
            .ReturnsAsync((ReceiptDTO?)null);
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);

        var controller = CreateController(receipts.Object, payments.Object);
        var result = await controller.Reprint(receiptId, registerId);
        Assert.IsType<NotFoundObjectResult>(result.Result);
        payments.Verify(
            x => x.ConfirmReceiptReprintAsync(
                It.IsAny<Guid>(),
                It.IsAny<ReceiptReprintRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Reprint_MatchingRegister_ConfirmsWithoutCreatingReceipt()
    {
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var dto = new ReceiptDTO
        {
            ReceiptId = receiptId,
            PaymentId = paymentId,
            CashRegisterId = registerId,
            ReceiptNumber = "AT-1",
        };
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerId)).ReturnsAsync(dto);
        var payments = new Mock<IPaymentService>();
        payments.Setup(x => x.ConfirmReceiptReprintAsync(
                paymentId,
                It.Is<ReceiptReprintRequest>(r => r.ReprintReasonCode == ReceiptReprintReasonCodes.CustomerRequest),
                "cashier-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiptReprintOperationResult
            {
                Success = true,
                Receipt = dto,
                AuditLogId = Guid.NewGuid(),
            });

        var controller = CreateController(receipts.Object, payments.Object);
        var result = await controller.Reprint(receiptId, registerId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReceiptReprintResponse>(ok.Value);
        Assert.Equal("Success", body.Outcome);
        Assert.Equal("ReceiptReprintConfirmed", body.ReportableEventType);
        Assert.Equal(receiptId, body.Receipt?.ReceiptId);
        payments.Verify(
            x => x.ConfirmReceiptReprintAsync(
                paymentId,
                It.IsAny<ReceiptReprintRequest>(),
                "cashier-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecentReceiptsForCashRegister_OnlyReturnsThatRegister()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosReceiptList_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var registerA = Guid.NewGuid();
        var registerB = Guid.NewGuid();
        SeedRegister(db, registerA, "K-A");
        SeedRegister(db, registerB, "K-B");
        var paymentA = SeedPayment(db, registerA, "R-A");
        var paymentB = SeedPayment(db, registerB, "R-B");
        SeedReceipt(db, paymentA, registerA, "R-A", 12.5m, SystemTenantIds.Platform);
        SeedReceipt(db, paymentB, registerB, "R-B", 99m, SystemTenantIds.Platform);
        await db.SaveChangesAsync();

        var service = CreateReceiptService(db);
        var listed = await service.GetRecentReceiptsForCashRegisterAsync(registerA, 20);

        Assert.Single(listed.Items);
        Assert.Equal("R-A", listed.Items[0].ReceiptNumber);
        Assert.Equal(registerA, listed.Items[0].CashRegisterEntityId);
        Assert.Equal(12.5m, listed.Items[0].GrandTotal);

        var other = await service.GetReceiptForCashRegisterAsync(listed.Items[0].ReceiptId, registerB);
        Assert.Null(other);
        var own = await service.GetReceiptForCashRegisterAsync(listed.Items[0].ReceiptId, registerA);
        Assert.NotNull(own);
        Assert.Equal(paymentA.Id, own!.PaymentId);
    }

    [Fact]
    public async Task GetReceiptForCashRegister_HidesOtherTenantReceipt()
    {
        var otherTenantId = Guid.NewGuid();
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosReceiptTenant_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);
        TenantTestDoubles.EnsureTenant(db, otherTenantId, "other-t");

        var ownRegister = Guid.NewGuid();
        var otherRegister = Guid.NewGuid();
        SeedRegister(db, ownRegister, "K-OWN");
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = otherTenantId,
            Id = otherRegister,
            RegisterNumber = "K-OTHER",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        var ownPayment = SeedPayment(db, ownRegister, "OWN-1");
        var otherPayment = SeedPayment(db, otherRegister, "OTH-1");
        SeedReceipt(db, ownPayment, ownRegister, "OWN-1", 5m, SystemTenantIds.Platform);
        var foreign = SeedReceipt(db, otherPayment, otherRegister, "OTH-1", 8m, otherTenantId);
        await db.SaveChangesAsync();

        var service = CreateReceiptService(db);
        Assert.Null(await service.GetReceiptForCashRegisterAsync(foreign.ReceiptId, otherRegister));
        Assert.Null(await service.GetReceiptForCashRegisterAsync(foreign.ReceiptId, ownRegister));
        var listed = await service.GetRecentReceiptsForCashRegisterAsync(ownRegister, 20);
        Assert.Single(listed.Items);
        Assert.Equal("OWN-1", listed.Items[0].ReceiptNumber);
    }

    private static PosReceiptsController CreateController(IReceiptService receipts, IPaymentService payments)
    {
        var controller = new PosReceiptsController(receipts, payments, NullLogger<PosReceiptsController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "cashier-1"),
            new(ClaimTypes.Role, "Cashier"),
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

    private static ReceiptService CreateReceiptService(AppDbContext db) =>
        new(
            db,
            NullLogger<ReceiptService>.Instance,
            Mock.Of<ITseService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "Test GmbH",
                TaxNumber = "ATU12345678",
            }),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment);

    private static void SeedRegister(AppDbContext db, Guid id, string number)
    {
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = id,
            RegisterNumber = number,
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
    }

    private static PaymentDetails SeedPayment(AppDbContext db, Guid registerId, string receiptNumber)
    {
        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            CashRegisterId = registerId,
            ReceiptNumber = receiptNumber,
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.PaymentDetails.Add(payment);
        return payment;
    }

    private static Receipt SeedReceipt(
        AppDbContext db,
        PaymentDetails payment,
        Guid registerId,
        string receiptNumber,
        decimal grandTotal,
        Guid tenantId)
    {
        var receipt = new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = payment.Id,
            ReceiptNumber = receiptNumber,
            IssuedAt = DateTime.UtcNow,
            CashierId = payment.CashierId,
            CashRegisterId = registerId,
            SubTotal = grandTotal,
            TaxTotal = 0,
            GrandTotal = grandTotal,
            CreatedAt = DateTime.UtcNow,
        };
        db.Receipts.Add(receipt);
        return receipt;
    }
}
