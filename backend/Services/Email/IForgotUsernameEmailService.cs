namespace KasseAPI_Final.Services.Email;

public interface IForgotUsernameEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendForgotUsernameAsync(
        ForgotUsernameEmailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ForgotUsernameEmailRequest(
    string ToEmail,
    IReadOnlyList<string> Usernames,
    /// <summary>Development-only footer for dev-mail capture (never sent via SMTP).</summary>
    string? DevAccountSummary = null);
