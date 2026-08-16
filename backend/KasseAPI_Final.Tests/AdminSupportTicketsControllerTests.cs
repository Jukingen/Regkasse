using KasseAPI_Final.Controllers;
using KasseAPI_Final.Services.Support;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminSupportTicketsControllerTests
{
    [Fact]
    public async Task List_WithoutAmbientTenant_ReturnsNotFound()
    {
        var tickets = new Mock<ISupportTicketService>(MockBehavior.Strict);
        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, (Guid?)null);

        var controller = CreateController(tickets.Object, accessor.Object);
        var result = await controller.List(cancellationToken: CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        tickets.Verify(
            x => x.ListForTenantAsync(
                It.IsAny<Guid>(),
                It.IsAny<SupportTicketListQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Get_CrossTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var tickets = new Mock<ISupportTicketService>();
        tickets
            .Setup(x => x.GetForTenantAsync(tenantId, otherId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ticket not found."));

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(tickets.Object, accessor.Object);
        var result = await controller.Get(otherId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateOwnStatus_CrossTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var tickets = new Mock<ISupportTicketService>();
        tickets
            .Setup(x => x.UpdateStatusForTenantAsync(
                tenantId,
                otherId,
                "Closed",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ticket not found."));

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(tickets.Object, accessor.Object);
        var result = await controller.UpdateOwnStatus(
            otherId,
            new UpdateSupportTicketStatusRequest { Status = "Closed" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static AdminSupportTicketsController CreateController(
        ISupportTicketService tickets,
        ICurrentTenantAccessor accessor)
    {
        return new AdminSupportTicketsController(tickets, accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}

public sealed class AdminSupportInboxControllerTests
{
    [Fact]
    public async Task Get_UnknownTicket_ReturnsNotFound()
    {
        var tickets = new Mock<ISupportTicketService>();
        var missing = Guid.NewGuid();
        tickets
            .Setup(x => x.GetAnyAsync(missing, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ticket not found."));

        var controller = new AdminSupportInboxController(tickets.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Get(missing, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_UnknownTicket_ReturnsNotFound()
    {
        var tickets = new Mock<ISupportTicketService>();
        var missing = Guid.NewGuid();
        tickets
            .Setup(x => x.UpdateStatusAsync(missing, "Resolved", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Ticket not found."));

        var controller = new AdminSupportInboxController(tickets.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.UpdateStatus(
            missing,
            new UpdateSupportTicketStatusRequest { Status = "Resolved" },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }
}
