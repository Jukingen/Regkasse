using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// FA shift open/close on <c>/api/admin/cash-registers/{id}/open|close</c> delegates to
/// <see cref="ICashRegisterShiftService"/> (same domain as legacy CashRegisterController).
/// </summary>
public sealed class AdminCashRegistersOpenCloseTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AdminCashRegistersController CreateController(
        ICashRegisterShiftService shift,
        string? actorUserId,
        ICashRegisterManagementService? management = null,
        string? actorRole = null)
    {
        var controller = new AdminCashRegistersController(
            Mock.Of<ICashRegisterDecommissionService>(),
            management ?? Mock.Of<ICashRegisterManagementService>(),
            Mock.Of<ICashRegisterListEnrichmentService>(),
            shift,
            CashRegisterTestDoubles.PermissiveRegisterPermissions(),
            TenantTestDoubles.TenantAccessorReturning(TenantAId),
            NullLogger<AdminCashRegistersController>.Instance,
            LocalizationTestDoubles.ApiMessageLocalizer());

        var claims = new List<Claim>();
        if (actorUserId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, actorUserId));
        if (!string.IsNullOrEmpty(actorRole))
            claims.Add(new Claim(ClaimTypes.Role, actorRole));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    claims.Count == 0
                        ? new ClaimsIdentity()
                        : new ClaimsIdentity(claims, "Test")),
            },
        };

        return controller;
    }

    private static CashRegisterDto ClosedRegister(Guid id) =>
        new()
        {
            Id = id,
            TenantId = TenantAId,
            RegisterNumber = "K1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

    private static CashRegisterDto DecommissionedRegister(Guid id) =>
        new()
        {
            Id = id,
            TenantId = TenantAId,
            RegisterNumber = "K1",
            Location = "Haupt",
            Status = RegisterStatus.Decommissioned,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = false,
        };

    [Fact]
    public async Task Open_DelegatesToShiftService_AndReturnsOk()
    {
        var registerId = Guid.NewGuid();
        var shift = new Mock<ICashRegisterShiftService>();
        shift
            .Setup(s => s.TryOpenCashRegisterAsync(
                registerId,
                "actor-1",
                0m,
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterOpenResult.Opened("K1"));

        var management = new Mock<ICashRegisterManagementService>();
        management
            .Setup(m => m.GetByIdAsync(registerId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedRegister(registerId));

        var controller = CreateController(shift.Object, "actor-1", management.Object);
        var result = await controller.Open(
            registerId,
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        shift.VerifyAll();
    }

    [Fact]
    public async Task Open_WithoutActor_ReturnsUnauthorized()
    {
        var controller = CreateController(Mock.Of<ICashRegisterShiftService>(), actorUserId: null);
        var result = await controller.Open(
            Guid.NewGuid(),
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Open_MissingRegister_ReturnsNotFound()
    {
        var registerId = Guid.NewGuid();
        var shift = new Mock<ICashRegisterShiftService>();
        var management = new Mock<ICashRegisterManagementService>();
        management
            .Setup(m => m.GetByIdAsync(registerId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashRegisterDto?)null);

        var controller = CreateController(shift.Object, "actor-1", management.Object);
        var result = await controller.Open(
            registerId,
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        shift.Verify(
            s => s.TryOpenCashRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Open_DecommissionedRegister_ReturnsBadRequest()
    {
        var registerId = Guid.NewGuid();
        var shift = new Mock<ICashRegisterShiftService>();
        var management = new Mock<ICashRegisterManagementService>();
        management
            .Setup(m => m.GetByIdAsync(registerId, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecommissionedRegister(registerId));

        var controller = CreateController(shift.Object, "actor-1", management.Object);
        var result = await controller.Open(
            registerId,
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        Assert.Contains("stillgelegt", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REGISTER_DECOMMISSIONED", json, StringComparison.Ordinal);
        shift.Verify(
            s => s.TryOpenCashRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Open_SuperAdmin_LooksUpWithoutTenantRestriction()
    {
        var registerId = Guid.NewGuid();
        var shift = new Mock<ICashRegisterShiftService>();
        shift
            .Setup(s => s.TryOpenCashRegisterAsync(
                registerId,
                "actor-1",
                0m,
                It.IsAny<string>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterOpenResult.Opened("K1"));

        var management = new Mock<ICashRegisterManagementService>();
        management
            .Setup(m => m.GetByIdAsync(registerId, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedRegister(registerId));

        var controller = CreateController(shift.Object, "actor-1", management.Object, Roles.SuperAdmin);
        var result = await controller.Open(
            registerId,
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        management.Verify(
            m => m.GetByIdAsync(registerId, null, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Close_DelegatesToShiftService_AndReturnsOk()
    {
        var registerId = Guid.NewGuid();
        var shift = new Mock<ICashRegisterShiftService>();
        shift
            .Setup(s => s.TryCloseCashRegisterAsync(
                registerId,
                "actor-1",
                42m,
                It.IsAny<CancellationToken>(),
                true))
            .ReturnsAsync(CashRegisterCloseResult.Success());

        var controller = CreateController(shift.Object, "actor-1");
        var result = await controller.Close(
            registerId,
            new CloseCashRegisterModel { ClosingBalance = 42m },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        shift.VerifyAll();
    }
}
