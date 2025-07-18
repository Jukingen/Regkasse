using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.Security.Claims;

namespace Registrierkasse_API.Middleware
{
    public enum UserRole
    {
        Cashier,
        Manager,
        Admin
    }

    public class RoleBasedAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RoleBasedAccessMiddleware> _logger;
        private readonly DemoUserLogService _demoLogService;

        public RoleBasedAccessMiddleware(
            RequestDelegate next, 
            ILogger<RoleBasedAccessMiddleware> logger,
            DemoUserLogService demoLogService)
        {
            _next = next;
            _logger = logger;
            _demoLogService = demoLogService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalPath = context.Request.Path;
            var method = context.Request.Method;
            var username = context.User?.Identity?.Name;

            // Demo kullanıcı kontrolü
            if (username?.StartsWith("demo.") == true)
            {
                var userRole = GetUserRole(context.User);
                var hasAccess = CheckAccess(userRole, originalPath, method);

                if (!hasAccess)
                {
                    await LogUnauthorizedAccess(username, originalPath, method, userRole);
                    
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Yetkisiz erişim",
                        message = "Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır.",
                        requiredRole = GetRequiredRole(originalPath),
                        userRole = userRole.ToString()
                    });
                    return;
                }

                // Demo kullanıcı işlemlerini logla
                await LogDemoUserAction(username, originalPath, method, userRole);
            }

            await _next(context);
        }

        private UserRole GetUserRole(ClaimsPrincipal user)
        {
            if (user.IsInRole("Admin"))
                return UserRole.Admin;
            if (user.IsInRole("Manager"))
                return UserRole.Manager;
            return UserRole.Cashier;
        }

        private bool CheckAccess(UserRole userRole, PathString path, string method)
        {
            // Admin her şeye erişebilir
            if (userRole == UserRole.Admin)
                return true;

            // Kasiyer erişim kuralları
            if (userRole == UserRole.Cashier)
            {
                // Kasiyer sadece satış, ürün, sepet, ödeme işlemlerine erişebilir
                var allowedPaths = new[]
                {
                    "/api/products",
                    "/api/categories", 
                    "/api/cart",
                    "/api/sales",
                    "/api/payment",
                    "/api/invoice",
                    "/api/customers",
                    "/api/barcode",
                    "/api/table"
                };

                var allowedMethods = new[] { "GET", "POST" };

                return allowedPaths.Any(p => path.StartsWithSegments(p)) && 
                       allowedMethods.Contains(method);
            }

            // Manager erişim kuralları
            if (userRole == UserRole.Manager)
            {
                // Manager kasiyer yetkileri + rapor ve denetim
                var allowedPaths = new[]
                {
                    "/api/products", "/api/categories", "/api/cart", "/api/sales",
                    "/api/payment", "/api/invoice", "/api/customers", "/api/barcode",
                    "/api/table", "/api/reports", "/api/audit", "/api/inventory"
                };

                return allowedPaths.Any(p => path.StartsWithSegments(p));
            }

            return false;
        }

        private string GetRequiredRole(PathString path)
        {
            if (path.StartsWithSegments("/api/users") || 
                path.StartsWithSegments("/api/roles") ||
                path.StartsWithSegments("/api/system") ||
                path.StartsWithSegments("/api/demo"))
                return "Admin";

            if (path.StartsWithSegments("/api/reports") || 
                path.StartsWithSegments("/api/audit"))
                return "Manager";

            return "Cashier";
        }

        private async Task LogUnauthorizedAccess(string username, PathString path, string method, UserRole userRole)
        {
            await _demoLogService.LogDemoUserAction(
                username,
                "UNAUTHORIZED_ACCESS",
                $"Erişim reddedildi: {method} {path}",
                "N/A"
            );

            _logger.LogWarning($"Demo user unauthorized access: {username} ({userRole}) tried to access {method} {path}");
        }

        private async Task LogDemoUserAction(string username, PathString path, string method, UserRole userRole)
        {
            await _demoLogService.LogDemoUserAction(
                username,
                "API_ACCESS",
                $"{method} {path}",
                "N/A"
            );
        }
    }
} 