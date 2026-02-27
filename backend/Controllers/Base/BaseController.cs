using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers.Base
{
    /// <summary>
    /// Tüm controller'lar için base class
    /// </summary>
    [ApiController]
    [Authorize]
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILogger _logger;

        protected BaseController(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcı ID'sini al
        /// </summary>
        protected string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// Kullanıcı rolünü al
        /// </summary>
        protected string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        /// <summary>
        /// Kullanıcının belirli role sahip olup olmadığını kontrol et
        /// </summary>
        protected bool HasRole(string role)
        {
            return User.IsInRole(role);
        }

        /// <summary>
        /// Kullanıcının belirli rollerden herhangi birine sahip olup olmadığını kontrol et
        /// </summary>
        protected bool HasAnyRole(params string[] roles)
        {
            return roles.Any(role => User.IsInRole(role));
        }

        /// <summary>
        /// Model validation kontrolü
        /// </summary>
        protected IActionResult? ValidateModel()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Model validation failed: {Errors}", string.Join("; ", errors));
                
                return BadRequest(new
                {
                    message = "Validation failed",
                    errors = errors
                });
            }
            return null;
        }

        /// <summary>
        /// Başarılı response oluştur
        /// </summary>
        protected IActionResult SuccessResponse<T>(T data, string message = "Operation completed successfully")
        {
            return Ok(new
            {
                success = true,
                message = message,
                data = data,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Hata response oluştur
        /// </summary>
        protected IActionResult ErrorResponse(string message, int statusCode = 400, object? details = null)
        {
            if (details != null)
            {
                var response = new
                {
                    success = false,
                    message = message,
                    details = details,
                    timestamp = DateTime.UtcNow
                };

                return statusCode switch
                {
                    400 => BadRequest(response),
                    401 => Unauthorized(response),
                    403 => Forbid(),
                    404 => NotFound(response),
                    409 => Conflict(response),
                    _ => StatusCode(statusCode, response)
                };
            }

            var simpleResponse = new
            {
                success = false,
                message = message,
                timestamp = DateTime.UtcNow
            };

            return statusCode switch
            {
                400 => BadRequest(simpleResponse),
                401 => Unauthorized(simpleResponse),
                403 => Forbid(),
                404 => NotFound(simpleResponse),
                409 => Conflict(simpleResponse),
                _ => StatusCode(statusCode, simpleResponse)
            };
        }

        /// <summary>
        /// Exception'ı handle et ve uygun response döndür
        /// </summary>
        protected IActionResult HandleException(Exception ex, string operation, string? userMessage = null)
        {
            _logger.LogError(ex, "Error in {Operation}: {Message}", operation, ex.Message);

            var message = userMessage ?? "An error occurred while processing your request";
            
            // Production'da detaylı hata bilgisi verme
            var details = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
                ? new { error = ex.Message, stackTrace = ex.StackTrace }
                : null;

            return ErrorResponse(message, 500, details);
        }

        /// <summary>
        /// Sayfalama parametrelerini doğrula
        /// </summary>
        protected (int pageNumber, int pageSize) ValidatePagination(int pageNumber, int pageSize, int maxPageSize = 100)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, Math.Min(pageSize, maxPageSize));
            
            return (pageNumber, pageSize);
        }

        /// <summary>
        /// Client IP adresini al
        /// </summary>
        protected string GetClientIpAddress()
        {
            try
            {
                var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                {
                    return forwardedHeader.Split(',')[0].Trim();
                }

                var remoteIp = HttpContext.Connection.RemoteIpAddress;
                return remoteIp?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// User Agent bilgisini al
        /// </summary>
        protected string GetUserAgent()
        {
            try
            {
                return Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Audit log için kullanıcı bilgilerini al
        /// </summary>
        protected (string userId, string userRole) GetAuditInfo()
        {
            var userId = GetCurrentUserId() ?? "Unknown";
            var userRole = GetCurrentUserRole() ?? "Unknown";
            return (userId, userRole);
        }
    }
}
