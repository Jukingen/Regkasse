using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Registrierkasse.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace Registrierkasse.Middleware
{
    public class AdminAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AdminAuthorizationMiddleware> _logger;

        public AdminAuthorizationMiddleware(RequestDelegate next, ILogger<AdminAuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            if (IsCriticalEndpoint(context.Request.Path))
            {
                var user = context.User;
                if (!user.Identity.IsAuthenticated)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }
                var userEmail = user.FindFirst(ClaimTypes.Email)?.Value;
                if (string.IsNullOrEmpty(userEmail))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Forbidden - User email not found");
                    return;
                }
                var appUser = await userManager.FindByEmailAsync(userEmail);
                if (appUser == null || appUser.Role != "Admin")
                {
                    _logger.LogWarning($"Non-admin user {userEmail} attempted to access critical endpoint {context.Request.Path}");
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Forbidden - Admin access required");
                    return;
                }
                _logger.LogInformation($"Admin user {userEmail} accessed critical endpoint {context.Request.Path}");
            }
            await _next(context);
        }

        private bool IsCriticalEndpoint(PathString path)
        {
            var criticalPaths = new[]
            {
                "/api/systemconfig",
                "/api/hardware",
                "/api/finanzonline",
                "/api/tse",
                "/api/audit",
                "/api/settings",
                "/api/company",
                "/api/users"
            };
            return criticalPaths.Any(criticalPath => path.StartsWithSegments(criticalPath, System.StringComparison.OrdinalIgnoreCase));
        }
    }
} 