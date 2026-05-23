using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services.Auth;

/// <summary>Auth flows that require tenant lifecycle checks beyond controller wiring.</summary>
public interface IAuthService
{
    /// <summary>
    /// Resolves login tenant snapshot or blocks when the user's mandant is soft-deleted / inaccessible.
    /// German operator message for disabled tenants (POS/FA display).
    /// </summary>
    Task<LoginTenantAccessResult> ResolveLoginTenantAccessAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

public sealed class LoginTenantAccessResult
{
    public bool Allowed { get; init; }
    public AuthTenantSnapshot? Snapshot { get; init; }
    public string? Message { get; init; }
    public string? Code { get; init; }

    public static LoginTenantAccessResult Ok(AuthTenantSnapshot snapshot) =>
        new() { Allowed = true, Snapshot = snapshot };

    public static LoginTenantAccessResult Blocked(string message, string code) =>
        new() { Allowed = false, Message = message, Code = code };
}
