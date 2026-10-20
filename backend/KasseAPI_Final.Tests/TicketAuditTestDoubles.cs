using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class TicketAuditTestDoubles
{
    public static Mock<IAuditLogService> Loose()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }
}
