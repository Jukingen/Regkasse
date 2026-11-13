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
        Assert.Equal(ReceiptListStatuses.Paid, listed.Items[0].Status);

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

    [Fact]
    public async Task GetRecent_PrefersLimitOverPageSize_AndClampsToMax()
    {
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetRecentReceiptsForCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedResult<ReceiptListItemDto>
            {
                Items = new List<ReceiptListItemDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
            });

        var registerId = Guid.NewGuid();
        var controller = CreateController(receipts.Object, Mock.Of<IPaymentService>());
        await controller.GetRecent(registerId, pageSize: 8, limit: 50);
        receipts.Verify(x => x.GetRecentReceiptsForCashRegisterAsync(registerId, 20), Times.Once);
    }

    [Fact]
    public async Task GetRecentReceiptsForCashRegister_MapsStornoAndRefundStatus()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosReceiptStatus_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var registerId = Guid.NewGuid();
        SeedRegister(db, registerId, "K-S");
        var paid = SeedPayment(db, registerId, "R-PAID");
        var storno = SeedPayment(db, registerId, "R-STO");
        storno.IsStorno = true;
        var refund = SeedPayment(db, registerId, "R-REF");
        refund.IsRefund = true;
        SeedReceipt(db, paid, registerId, "R-PAID", 10m, SystemTenantIds.Platform);
        SeedReceipt(db, storno, registerId, "R-STO", -10m, SystemTenantIds.Platform);
        SeedReceipt(db, refund, registerId, "R-REF", -4m, SystemTenantIds.Platform);
        await db.SaveChangesAsync();

        var service = CreateReceiptService(db);
        var listed = await service.GetRecentReceiptsForCashRegisterAsync(registerId, 20);
        Assert.Equal(3, listed.Items.Count);
        Assert.Equal(ReceiptListStatuses.Storno, listed.Items.Single(i => i.ReceiptNumber == "R-STO").Status);
        Assert.Equal(ReceiptListStatuses.Refund, listed.Items.Single(i => i.ReceiptNumber == "R-REF").Status);
        Assert.Equal(ReceiptListStatuses.Paid, listed.Items.Single(i => i.ReceiptNumber == "R-PAID").Status);
    }

    [Fact]
    public async Task GetRecentReceiptsForCashRegister_MarksOriginalAsStornoWhenChildExists()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosReceiptChildStorno_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var registerId = Guid.NewGuid();
        SeedRegister(db, registerId, "K-C");
        var original = SeedPayment(db, registerId, "R-ORIG");
        var child = SeedPayment(db, registerId, "R-STO-C");
        child.IsStorno = true;
        child.OriginalPaymentId = original.Id;
        SeedReceipt(db, original, registerId, "R-ORIG", 18m, SystemTenantIds.Platform);
        SeedReceipt(db, child, registerId, "R-STO-C", -18m, SystemTenantIds.Platform);
        await db.SaveChangesAsync();

        var service = CreateReceiptService(db);
        var listed = await service.GetRecentReceiptsForCashRegisterAsync(registerId, 20);
        Assert.Equal(ReceiptListStatuses.Storno, listed.Items.Single(i => i.ReceiptNumber == "R-ORIG").Status);
        Assert.Equal(ReceiptListStatuses.Storno, listed.Items.Single(i => i.ReceiptNumber == "R-STO-C").Status);
    }

    [Fact]
    public async Task Cancel_WrongRegister_ReturnsNotFound_AndDoesNotCallFiskaly()
    {
        var receiptId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerId))
            .ReturnsAsync((ReceiptDTO?)null);
        var fiskaly = new Mock<IFiskalyReceiptService>(MockBehavior.Strict);

        var controller = CreateController(receipts.Object, Mock.Of<IPaymentService>(), fiskaly.Object);
        var result = await controller.Cancel(receiptId, registerId, new PosReceiptCancelRequest());
        Assert.IsType<NotFoundObjectResult>(result.Result);
        fiskaly.Verify(
            x => x.CancelReceiptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_OtherCashiersReceipt_ReturnsNotOwner()
    {
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerId))
            .ReturnsAsync(new ReceiptDTO
            {
                ReceiptId = receiptId,
                PaymentId = paymentId,
                CashRegisterId = registerId,
                CashierId = "other-cashier",
                Date = DateTime.UtcNow,
                GrandTotal = 12m,
            });
        var payments = new Mock<IPaymentService>();
        payments.Setup(x => x.GetPaymentAsync(paymentId))
            .ReturnsAsync(new PaymentDetails
            {
                Id = paymentId,
                CashierId = "other-cashier",
                TotalAmount = 12m,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        var fiskaly = new Mock<IFiskalyReceiptService>(MockBehavior.Strict);

        var controller = CreateController(receipts.Object, payments.Object, fiskaly.Object);
        var result = await controller.Cancel(receiptId, registerId, new PosReceiptCancelRequest());
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<StornoResponse>(bad.Value);
        Assert.Equal(PosReceiptStornoEligibility.NotOwnerErrorKey, body.ErrorKey);
    }

    [Fact]
    public async Task Cancel_OwnReceiptToday_DelegatesToFiskalyCancel()
    {
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var stornoId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var receipts = new Mock<IReceiptService>();
        receipts.Setup(x => x.GetReceiptForCashRegisterAsync(receiptId, registerId))
            .ReturnsAsync(new ReceiptDTO
            {
                ReceiptId = receiptId,
                PaymentId = paymentId,
                CashRegisterId = registerId,
                CashierId = "cashier-1",
                Date = DateTime.UtcNow,
                GrandTotal = 12m,
                ReceiptNumber = "AT-1",
            });
        var payments = new Mock<IPaymentService>();
        payments.Setup(x => x.GetPaymentAsync(paymentId))
            .ReturnsAsync(new PaymentDetails
            {
                Id = paymentId,
                CashierId = "cashier-1",
                TotalAmount = 12m,
                ReceiptNumber = "AT-1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        var fiskaly = new Mock<IFiskalyReceiptService>();
        fiskaly.Setup(x => x.CancelReceiptAsync(
                registerId,
                paymentId,
                PosReceiptStornoEligibility.DefaultReason,
                "cashier-1",
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto
            {
                ReceiptId = stornoId.ToString("D"),
                ReceiptNumber = "AT-2",
            }));
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogPaymentOperationAsync(
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

        var controller = CreateController(receipts.Object, payments.Object, fiskaly.Object, audit.Object);
        var result = await controller.Cancel(receiptId, registerId, new PosReceiptCancelRequest());
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<StornoResponse>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal(stornoId, body.StornoPaymentId);
    }

    private static PosReceiptsController CreateController(
        IReceiptService receipts,
        IPaymentService payments,
        IFiskalyReceiptService? fiskaly = null,
        IAuditLogService? audit = null)
    {
        var controller = new PosReceiptsController(
            receipts,
            payments,
            fiskaly ?? Mock.Of<IFiskalyReceiptService>(),
            Mock.Of<IUserService>(),
            audit ?? Mock.Of<IAuditLogService>(),
            NullLogger<PosReceiptsController>.Instance);
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
