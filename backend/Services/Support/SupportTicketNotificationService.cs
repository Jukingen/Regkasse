using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Services.Support;

public sealed class SupportTicketNotificationService : ISupportTicketNotificationService
{
    private readonly IActivityEventPublisher _activity;
    private readonly IDataDeletionNotificationSender _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SupportTicketNotificationService> _logger;

    public SupportTicketNotificationService(
        IActivityEventPublisher activity,
        IDataDeletionNotificationSender email,
        UserManager<ApplicationUser> userManager,
        ILogger<SupportTicketNotificationService> logger)
    {
        _activity = activity;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task NotifyNewTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        await _activity.TryPublishAsync(
            ticket.TenantId,
            ActivityEventType.SupportTicketCreated,
            metadata: new
            {
                TicketId = ticket.Id.ToString("D"),
                ticket.TicketNumber,
                ticket.Title,
                ticket.Category,
                ticket.Priority,
            },
            actorUserId: ticket.CreatedByUserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifySuperAdminsAsync(
            subject: $"New support ticket: {ticket.Title}",
            body: $"A Mandanten-Admin opened a {ticket.Category} ticket ({ticket.Priority}).\n\n{ticket.Title}",
            ticket,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task NotifyStaffReplyAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        await _activity.TryPublishAsync(
            ticket.TenantId,
            ActivityEventType.SupportTicketStaffReplied,
            metadata: new { TicketId = ticket.Id.ToString("D"), ticket.Title },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var creator = await _userManager.FindByIdAsync(ticket.CreatedByUserId).ConfigureAwait(false);
        if (creator?.EmailConfirmed == true && !string.IsNullOrWhiteSpace(creator.Email))
        {
            await _email.SendAsync(
                to: [creator.Email],
                cc: Array.Empty<string>(),
                subject: $"Support reply: {ticket.Title}",
                plainBody: "The Regkasse team replied to your support ticket.",
                ct: cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NotifyTenantReplyAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        await _activity.TryPublishAsync(
            ticket.TenantId,
            ActivityEventType.SupportTicketTenantReplied,
            metadata: new { TicketId = ticket.Id.ToString("D"), ticket.Title },
            actorUserId: ticket.CreatedByUserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await NotifySuperAdminsAsync(
            subject: $"Support ticket reply: {ticket.Title}",
            body: "The mandant added a message to an open support ticket.",
            ticket,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task NotifyResolvedAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        await _activity.TryPublishAsync(
            ticket.TenantId,
            ActivityEventType.SupportTicketResolved,
            metadata: new
            {
                TicketId = ticket.Id.ToString("D"),
                ticket.TicketNumber,
                ticket.Title,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var creator = await _userManager.FindByIdAsync(ticket.CreatedByUserId).ConfigureAwait(false);
        if (creator?.EmailConfirmed == true && !string.IsNullOrWhiteSpace(creator.Email))
        {
            await _email.SendAsync(
                to: [creator.Email],
                cc: Array.Empty<string>(),
                subject: $"Support ticket resolved: {ticket.Title}",
                plainBody: $"Your support ticket {ticket.TicketNumber} was marked as resolved.",
                ct: cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NotifyClosedAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        await _activity.TryPublishAsync(
            ticket.TenantId,
            ActivityEventType.SupportTicketClosed,
            metadata: new
            {
                TicketId = ticket.Id.ToString("D"),
                ticket.TicketNumber,
                ticket.Title,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var creator = await _userManager.FindByIdAsync(ticket.CreatedByUserId).ConfigureAwait(false);
        if (creator?.EmailConfirmed == true && !string.IsNullOrWhiteSpace(creator.Email))
        {
            await _email.SendAsync(
                to: [creator.Email],
                cc: Array.Empty<string>(),
                subject: $"Support ticket closed: {ticket.Title}",
                plainBody: $"Your support ticket {ticket.TicketNumber} was closed.",
                ct: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task NotifySuperAdminsAsync(
        string subject,
        string body,
        SupportTicket ticket,
        CancellationToken cancellationToken)
    {
        try
        {
            var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
            var emails = superAdmins
                .Where(u => u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (emails.Count == 0)
                return;

            await _email.SendAsync(
                to: emails,
                cc: Array.Empty<string>(),
                subject: subject,
                plainBody: body,
                ct: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Support ticket email notify failed. TicketId={TicketId}",
                ticket.Id);
        }
    }
}
