using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Kullanıcı işlemleri için service interface
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Kullanıcıyı ID'ye göre getir
        /// </summary>
        Task<ApplicationUser?> GetUserByIdAsync(string? userId);
        
        /// <summary>
        /// Kullanıcıyı email'e göre getir
        /// </summary>
        Task<ApplicationUser?> GetUserByEmailAsync(string email);
        
        /// <summary>
        /// Kullanıcı rolünü kontrol et
        /// </summary>
        Task<bool> HasRoleAsync(string userId, string role);
        
        /// <summary>
        /// Kullanıcı yetkilerini kontrol et
        /// </summary>
        Task<bool> HasPermissionAsync(string userId, string permission);
    }
}
