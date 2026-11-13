using KasseAPI_Final.Security;

namespace KasseAPI_Final.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentUserId()
    {
        var raw = _httpContextAccessor.HttpContext?.User.GetActorUserId();
        return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
    }
}
