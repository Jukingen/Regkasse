namespace KasseAPI_Final.Services;

/// <summary>
/// Kritik hesap değişikliklerinde oturum iptali (refresh token revoke).
/// RefreshToken tablosu eklendiğinde gerçek implementasyon yapılır.
/// </summary>
public interface IUserSessionInvalidation
{
    /// <summary>
    /// Kullanıcının tüm oturumlarını iptal et (deactivate, role change, password reset sonrası).
    /// </summary>
    Task InvalidateSessionsForUserAsync(string userId, CancellationToken cancellationToken = default);
}
