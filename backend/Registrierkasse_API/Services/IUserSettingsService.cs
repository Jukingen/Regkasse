using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public interface IUserSettingsService
    {
        Task<UserSettings> GetUserSettingsAsync(string userId);
        Task<UserSettings> UpdateUserSettingsAsync(string userId, object settings);
        Task<UserSettings> ResetUserSettingsAsync(string userId);
    }
} 