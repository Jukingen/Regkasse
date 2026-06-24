namespace KasseAPI_Final.Services;

/// <summary>Resolves the authenticated actor user id from the current HTTP request.</summary>
public interface ICurrentUserService
{
    Guid GetCurrentUserId();
}
