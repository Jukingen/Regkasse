using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Support;

public interface ISupportTicketNotificationService
{
    Task NotifyNewTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default);

    Task NotifyStaffReplyAsync(SupportTicket ticket, CancellationToken cancellationToken = default);

    Task NotifyTenantReplyAsync(SupportTicket ticket, CancellationToken cancellationToken = default);

    Task NotifyResolvedAsync(SupportTicket ticket, CancellationToken cancellationToken = default);

    Task NotifyClosedAsync(SupportTicket ticket, CancellationToken cancellationToken = default);
}
