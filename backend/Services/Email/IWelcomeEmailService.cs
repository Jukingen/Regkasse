namespace KasseAPI_Final.Services.Email;

/// <summary>Sends tenant onboarding welcome emails when SMTP is configured.</summary>
public interface IWelcomeEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendWelcomeAsync(
        WelcomeEmailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WelcomeEmailRequest(
    string ToEmail,
    string TenantName,
    string TenantSlug,
    string PortalUrl,
    string AdminEmail,
    string TemporaryPassword,
    bool ForcePasswordChangeOnNextLogin);
