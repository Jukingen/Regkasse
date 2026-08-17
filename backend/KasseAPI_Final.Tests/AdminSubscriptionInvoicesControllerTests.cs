using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminSubscriptionInvoicesControllerTests
{
    [Fact]
    public void Controller_RequiresSuperAdmin()
    {
        var type = typeof(AdminSubscriptionInvoicesController);
        var authorize = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.Contains(authorize, a => a.Roles == Roles.SuperAdmin);
    }

    [Fact]
    public async Task MarkAsPaid_WithoutActor_ReturnsUnauthorized()
    {
        var invoices = new Mock<ISubscriptionInvoiceService>(MockBehavior.Strict);
        var controller = CreateController(invoices.Object, actorUserId: null);

        var result = await controller.MarkAsPaid(Guid.NewGuid(), new MarkPaidRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task MarkAsPaid_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var invoices = new Mock<ISubscriptionInvoiceService>();
        invoices
            .Setup(x => x.MarkPaidAsync(id, It.IsAny<MarkPaidRequest>(), actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionInvoiceActionResult.Fail(SubscriptionInvoiceService.NotFoundCode, "Invoice not found."));

        var controller = CreateController(invoices.Object, actor);
        var result = await controller.MarkAsPaid(id, new MarkPaidRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MarkAsPaid_Succeeded_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var dto = new SubscriptionInvoiceDto
        {
            Id = id,
            Status = "paid",
            InvoiceNumber = "SUB-1",
            LicenseType = LicenseType.Starter,
        };
        var invoices = new Mock<ISubscriptionInvoiceService>();
        invoices
            .Setup(x => x.MarkPaidAsync(id, It.IsAny<MarkPaidRequest>(), actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionInvoiceActionResult.Ok(dto));

        var controller = CreateController(invoices.Object, actor);
        var result = await controller.MarkAsPaid(id, new MarkPaidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SubscriptionInvoiceDto>(ok.Value);
        Assert.Equal("paid", body.Status);
    }

    [Fact]
    public async Task VoidInvoice_Paid_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var invoices = new Mock<ISubscriptionInvoiceService>();
        invoices
            .Setup(x => x.VoidAsync(id, It.IsAny<VoidInvoiceRequest>(), actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionInvoiceActionResult.Fail(
                SubscriptionInvoiceService.PaidCannotVoidCode,
                "Cannot void a paid invoice."));

        var controller = CreateController(invoices.Object, actor);
        var result = await controller.VoidInvoice(
            id,
            new VoidInvoiceRequest { Reason = "x" },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    private static AdminSubscriptionInvoicesController CreateController(
        ISubscriptionInvoiceService invoices,
        Guid? actorUserId)
    {
        var controller = new AdminSubscriptionInvoicesController(invoices)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = actorUserId is Guid id
                        ? new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, id.ToString("D"))],
                            "Test"))
                        : new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };
        return controller;
    }
}
