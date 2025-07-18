using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public class DemoUserLogService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DemoUserLogService> _logger;

        public DemoUserLogService(AppDbContext context, ILogger<DemoUserLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogDemoUserAction(string username, string action, string details, string ipAddress = "")
        {
            try
            {
                var demoLog = new Models.DemoUserLog
                {
                    Username = username,
                    Action = action,
                    Details = details,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow,
                    UserType = username.StartsWith("demo.cashier") ? "Cashier" : "Admin"
                };

                _context.DemoUserLogs.Add(demoLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Demo user action logged: {username} - {action} - {details}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging demo user action: {username} - {action}");
            }
        }

        public async Task<List<Models.DemoUserLog>> GetDemoUserLogs(string username = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.DemoUserLogs.AsQueryable();

            if (!string.IsNullOrEmpty(username))
            {
                query = query.Where(l => l.Username == username);
            }

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.Timestamp <= endDate.Value);
            }

            return await query
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<DemoUserActivitySummary>> GetDemoUserActivitySummary()
        {
            var summary = await _context.DemoUserLogs
                .GroupBy(l => new { l.Username, l.UserType })
                .Select(g => new DemoUserActivitySummary
                {
                    Username = g.Key.Username,
                    UserType = g.Key.UserType,
                    TotalActions = g.Count(),
                    LastActivity = g.Max(l => l.Timestamp),
                    MostCommonAction = g.GroupBy(l => l.Action)
                        .OrderByDescending(x => x.Count())
                        .First().Key
                })
                .ToListAsync();

            return summary;
        }

        public async Task<List<DemoUserActionStats>> GetDemoUserActionStats()
        {
            var stats = await _context.DemoUserLogs
                .GroupBy(l => l.Action)
                .Select(g => new DemoUserActionStats
                {
                    Action = g.Key,
                    TotalCount = g.Count(),
                    CashierCount = g.Count(l => l.UserType == "Cashier"),
                    AdminCount = g.Count(l => l.UserType == "Admin"),
                    LastUsed = g.Max(l => l.Timestamp)
                })
                .OrderByDescending(s => s.TotalCount)
                .ToListAsync();

            return stats;
        }
    }

    public class DemoUserActivitySummary
    {
        public string Username { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public int TotalActions { get; set; }
        public DateTime LastActivity { get; set; }
        public string MostCommonAction { get; set; } = string.Empty;
    }

    public class DemoUserActionStats
    {
        public string Action { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int CashierCount { get; set; }
        public int AdminCount { get; set; }
        public DateTime LastUsed { get; set; }
    }
} 