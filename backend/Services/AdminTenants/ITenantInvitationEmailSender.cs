namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Sends tenant user invitation emails via configured SMTP (<see cref="Configuration.EmailSmtpOptions"/>).</summary>
public interface ITenantInvitationEmailSender
{
    bool IsConfigured { get; }

    Task<bool> TrySendInvitationAsync(
        string toEmail,
        string subject,
        string plainBody,
        CancellationToken cancellationToken = default);
}
