using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Text.Json;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// Demo kullanıcı yönetim servisi - Test kullanıcıları oluşturur ve yönetir
    /// </summary>
    public class DemoUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly AuthorizationService _authService;
        private readonly ILogger<DemoUserService> _logger;

        public DemoUserService(UserManager<ApplicationUser> userManager, AppDbContext context, 
            AuthorizationService authService, ILogger<DemoUserService> logger)
        {
            _userManager = userManager;
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Demo kullanıcıları oluşturur - Admin ve Kasiyer rolleri ile
        /// </summary>
        public async Task CreateDemoUsersAsync()
        {
            try
            {
                // Admin demo kullanıcıları
                await CreateDemoUserAsync("admin1@demo.com", "Admin123!", "Admin", "Demo", "ADMIN001", "Admin");
                await CreateDemoUserAsync("admin2@demo.com", "Admin123!", "Manager", "Demo", "ADMIN002", "Admin");

                // Kasiyer demo kullanıcıları
                await CreateDemoUserAsync("cashier1@demo.com", "Cashier123!", "Ahmet", "Kasiyer", "CASH001", "Cashier");
                await CreateDemoUserAsync("cashier2@demo.com", "Cashier123!", "Ayşe", "Kasiyer", "CASH002", "Cashier");
                await CreateDemoUserAsync("cashier3@demo.com", "Cashier123!", "Mehmet", "Kasiyer", "CASH003", "Cashier");

                _logger.LogInformation("Demo users created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating demo users");
            }
        }

        /// <summary>
        /// Tekil demo kullanıcı oluşturur
        /// </summary>
        private async Task CreateDemoUserAsync(string email, string password, string firstName, string lastName, 
            string employeeNumber, string roleName)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                _logger.LogInformation($"Demo user {email} already exists");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmployeeNumber = employeeNumber,
                AccountType = "demo",
                IsDemo = true,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Varsayılan rol ata
                await _authService.AssignDefaultRoleAsync(user.Id, roleName);

                _logger.LogInformation($"Demo user created: {email} with role {roleName}");
            }
            else
            {
                _logger.LogError($"Failed to create demo user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        /// <summary>
        /// Demo kullanıcı aktivitesini loglar
        /// </summary>
        public async Task LogDemoUserActivityAsync(string username, string action, string details)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null) return;

                var demoLog = new DemoUserLog
                {
                    Username = username,
                    Action = action,
                    Details = details,
                    IpAddress = "demo",
                    Timestamp = DateTime.UtcNow,
                    UserType = await _authService.GetUserRoleAsync(user.Id)
                };

                _context.DemoUserLogs.Add(demoLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Demo user activity logged: {username} - {action} - {details}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging demo user activity: {username} - {action}");
            }
        }

        /// <summary>
        /// Demo kullanıcı istatistiklerini döndürür
        /// </summary>
        public async Task<object> GetDemoUserStatsAsync()
        {
            var stats = await _context.DemoUserLogs
                .GroupBy(l => l.UserType)
                .Select(g => new
                {
                    Role = g.Key,
                    TotalActions = g.Count(),
                    LastActivity = g.Max(l => l.Timestamp),
                    MostCommonAction = g.GroupBy(l => l.Action)
                        .OrderByDescending(x => x.Count())
                        .First().Key
                })
                .ToListAsync();

            return new { DemoUserStats = stats };
        }

        /// <summary>
        /// Demo kullanıcıları listeler
        /// </summary>
        public async Task<List<object>> GetDemoUsersAsync()
        {
            var demoUsers = await _userManager.Users
                .Where(u => u.IsDemo)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.EmployeeNumber,
                    u.AccountType,
                    u.IsActive
                })
                .ToListAsync();

            return demoUsers.Cast<object>().ToList();
        }

        /// <summary>
        /// Demo kullanıcıları siler
        /// </summary>
        public async Task DeleteDemoUsersAsync()
        {
            try
            {
                var demoUsers = await _userManager.Users.Where(u => u.IsDemo).ToListAsync();
                
                foreach (var user in demoUsers)
                {
                    var result = await _userManager.DeleteAsync(user);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"Demo user deleted: {user.Email}");
                    }
                    else
                    {
                        _logger.LogError($"Failed to delete demo user {user.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }

                _logger.LogInformation($"Deleted {demoUsers.Count} demo users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting demo users");
            }
        }
    }
} 