namespace KasseAPI_Final.Services.Email;

/// <summary>Sends notification when an admin changes a user's login username.</summary>
public interface IUsernameChangeEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendUsernameChangedAsync(
        UsernameChangedEmailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record UsernameChangedEmailRequest(
    string ToEmail,
    string OldUsername,
    string NewUsername,
    string ChangedByAdminEmail,
    DateTime ChangedAtUtc);
