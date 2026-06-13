namespace KasseAPI_Final.Services.Email;

public interface IForgotPasswordEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendForgotPasswordAsync(
        ForgotPasswordEmailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ForgotPasswordEmailRequest(
    string ToEmail,
    string ResetToken);
