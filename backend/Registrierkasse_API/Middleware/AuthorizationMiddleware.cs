using Microsoft.AspNetCore.Http;
using Registrierkasse_API.Services;
using System.Security.Claims;

namespace Registrierkasse_API.Middleware
{
    /// <summary>
    /// Gelişmiş yetki kontrol middleware'i - Rol tabanlı erişim kontrolü
    /// </summary>
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AuthorizationService _authService;
        private readonly ILogger<AuthorizationMiddleware> _logger;

        public AuthorizationMiddleware(RequestDelegate next, AuthorizationService authService, ILogger<AuthorizationMiddleware> logger)
        {
            _next = next;
            _authService = authService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                await _next(context);
                return;
            }

            // Yetki kontrolü gerektiren endpoint'leri kontrol et
            var requiresAuth = endpoint.Metadata.GetMetadata<RequirePermissionAttribute>();
            if (requiresAuth == null)
            {
                await _next(context);
                return;
            }

            var user = context.User;
            if (!user.Identity?.IsAuthenticated == true)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Sie müssen sich anmelden." });
                return;
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Ungültige Benutzeridentität." });
                return;
            }

            // Yetki kontrolü
            var hasPermission = await _authService.HasPermissionAsync(userId, requiresAuth.Resource, requiresAuth.Action);
            if (!hasPermission)
            {
                var errorMessage = await _authService.HandleUnauthorizedAccessAsync(requiresAuth.Resource, requiresAuth.Action);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = errorMessage });
                return;
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Yetki kontrolü için attribute - Controller ve Action'larda kullanılır
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequirePermissionAttribute : Attribute
    {
        public string Resource { get; }
        public string Action { get; }

        public RequirePermissionAttribute(string resource, string action)
        {
            Resource = resource;
            Action = action;
        }
    }

    /// <summary>
    /// Admin yetkisi gerektiren attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireAdminAttribute : RequirePermissionAttribute
    {
        public RequireAdminAttribute(string resource, string action) : base(resource, action)
        {
        }
    }

    /// <summary>
    /// Kasiyer yetkisi gerektiren attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireCashierAttribute : RequirePermissionAttribute
    {
        public RequireCashierAttribute(string resource, string action) : base(resource, action)
        {
        }
    }
} 