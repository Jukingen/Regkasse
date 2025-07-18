using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse_API.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserSettingsService> _logger;

        public UserSettingsService(AppDbContext context, ILogger<UserSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserSettings> GetUserSettingsAsync(string userId)
        {
            try
            {
                var settings = await _context.UserSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    // Varsayılan ayarları oluştur
                    settings = new UserSettings
                    {
                        UserId = userId,
                        Language = "de-DE",
                        Theme = "light"
                    };

                    _context.UserSettings.Add(settings);
                    await _context.SaveChangesAsync();
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user settings for user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserSettings> UpdateUserSettingsAsync(string userId, object settings)
        {
            try
            {
                var existingSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (existingSettings == null)
                {
                    existingSettings = new UserSettings { UserId = userId };
                    _context.UserSettings.Add(existingSettings);
                }

                // Settings güncelleme işlemi burada yapılacak
                // Şimdilik basit bir implementasyon

                await _context.SaveChangesAsync();
                return existingSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user settings for user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserSettings> ResetUserSettingsAsync(string userId)
        {
            try
            {
                var settings = await _context.UserSettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings != null)
                {
                    // Varsayılan değerlere sıfırla
                    settings.Language = "de-DE";
                    settings.Theme = "light";

                    await _context.SaveChangesAsync();
                }

                return settings ?? await GetUserSettingsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset user settings for user {UserId}", userId);
                throw;
            }
        }
    }
} 