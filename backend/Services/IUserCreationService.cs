namespace KasseAPI_Final.Services;

/// <summary>Resolves login usernames for admin user creation flows.</summary>
public interface IUserCreationService
{
    /// <summary>
    /// Uses <paramref name="requestedUserName"/> when provided (must be unique);
    /// otherwise allocates the next role-based name (manager1, user2, …).
    /// </summary>
    Task<(string UserName, string? Error)> ResolveUsernameAsync(
        string? requestedUserName,
        string role,
        CancellationToken cancellationToken = default);
}
