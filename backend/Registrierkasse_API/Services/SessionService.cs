using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse.Services
{
    public interface ISessionService
    {
        Task<bool> ValidateSessionAsync(string userId, string sessionId);
        Task<string> CreateSessionAsync(ApplicationUser user, string deviceInfo);
        Task<bool> InvalidateSessionAsync(string userId, string sessionId);
        Task<bool> InvalidateAllSessionsAsync(string userId);
        Task<bool> IsSessionActiveAsync(string userId, string sessionId);
        Task<DateTime?> GetLastActivityAsync(string userId, string sessionId);
        Task UpdateLastActivityAsync(string userId, string sessionId);
    }

    public class SessionService : ISessionService
    {
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _context;
        private readonly ILogger<SessionService> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> _sessionActivities = new();

        private const string SESSION_KEY_PREFIX = "session_";
        private const int SESSION_TIMEOUT_MINUTES = 30;
        private const int MAX_SESSIONS_PER_USER = 3;

        public SessionService(
            IMemoryCache cache,
            AppDbContext context,
            ILogger<SessionService> logger)
        {
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ValidateSessionAsync(string userId, string sessionId)
        {
            try
            {
                var cacheKey = $"{SESSION_KEY_PREFIX}{userId}_{sessionId}";
                
                if (!_cache.TryGetValue(cacheKey, out bool isValid))
                {
                    // Session not found in cache, check database
                    var session = await _context.UserSessions
                        .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionId == sessionId);

                    if (session == null || !session.IsActive || session.ExpiresAt < DateTime.UtcNow)
                    {
                        return false;
                    }

                    isValid = true;
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));
                    
                    _cache.Set(cacheKey, isValid, cacheOptions);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session for user {UserId}", userId);
                return false;
            }
        }

        public async Task<string> CreateSessionAsync(ApplicationUser user, string deviceInfo)
        {
            try
            {
                // Check active session count
                var activeSessions = await _context.UserSessions
                    .CountAsync(s => s.UserId == user.Id && s.IsActive);

                if (activeSessions >= MAX_SESSIONS_PER_USER)
                {
                    // Invalidate oldest session
                    var oldestSession = await _context.UserSessions
                        .Where(s => s.UserId == user.Id && s.IsActive)
                        .OrderBy(s => s.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (oldestSession != null)
                    {
                        oldestSession.IsActive = false;
                        await _context.SaveChangesAsync();
                    }
                }

                var sessionId = Guid.NewGuid().ToString();
                var session = new UserSession
                {
                    UserId = user.Id,
                    SessionId = sessionId,
                    DeviceInfo = deviceInfo,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(SESSION_TIMEOUT_MINUTES),
                    IsActive = true,
                    LastActivity = DateTime.UtcNow
                };

                _context.UserSessions.Add(session);
                await _context.SaveChangesAsync();

                var cacheKey = $"{SESSION_KEY_PREFIX}{user.Id}_{sessionId}";
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));

                _cache.Set(cacheKey, true, cacheOptions);
                _sessionActivities[$"{user.Id}_{sessionId}"] = DateTime.UtcNow;

                _logger.LogInformation("New session created for user {UserId} from device {DeviceInfo}", 
                    user.Id, deviceInfo);

                return sessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session for user {UserId}", user.Id);
                throw;
            }
        }

        public async Task<bool> InvalidateSessionAsync(string userId, string sessionId)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionId == sessionId);

                if (session != null)
                {
                    session.IsActive = false;
                    await _context.SaveChangesAsync();
                }

                var cacheKey = $"{SESSION_KEY_PREFIX}{userId}_{sessionId}";
                _cache.Remove(cacheKey);
                _sessionActivities.TryRemove($"{userId}_{sessionId}", out _);

                _logger.LogInformation("Session invalidated for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating session for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> InvalidateAllSessionsAsync(string userId)
        {
            try
            {
                var sessions = await _context.UserSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.IsActive = false;
                    var cacheKey = $"{SESSION_KEY_PREFIX}{userId}_{session.SessionId}";
                    _cache.Remove(cacheKey);
                    _sessionActivities.TryRemove($"{userId}_{session.SessionId}", out _);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("All sessions invalidated for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all sessions for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> IsSessionActiveAsync(string userId, string sessionId)
        {
            return await ValidateSessionAsync(userId, sessionId);
        }

        public Task<DateTime?> GetLastActivityAsync(string userId, string sessionId)
        {
            var key = $"{userId}_{sessionId}";
            return Task.FromResult(_sessionActivities.TryGetValue(key, out var lastActivity) 
                ? (DateTime?)lastActivity 
                : null);
        }

        public Task UpdateLastActivityAsync(string userId, string sessionId)
        {
            var key = $"{userId}_{sessionId}";
            _sessionActivities[key] = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }
} 