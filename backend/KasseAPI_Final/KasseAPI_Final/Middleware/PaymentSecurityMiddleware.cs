using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq; // Added for FirstOrDefault

namespace KasseAPI_Final.Middleware
{
    /// <summary>
    /// Middleware for enhancing security of payment cancellation/modification API requests
    /// This middleware validates payment operations and prevents unauthorized modifications
    /// </summary>
    public class PaymentSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PaymentSecurityMiddleware> _logger;

        public PaymentSecurityMiddleware(RequestDelegate next, ILogger<PaymentSecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;
            var requestPath = context.Request.Path.Value?.ToLower();
            var requestMethod = context.Request.Method;

            // Check if this is a payment-related request that requires security validation
            if (IsPaymentSecurityEndpoint(requestPath, requestMethod))
            {
                try
                {
                    _logger.LogInformation("Payment security middleware processing request: {Method} {Path} from IP: {IP}", 
                        requestMethod, requestPath, GetClientIpAddress(context));

                    // Validate request headers and authentication
                    if (!await ValidatePaymentRequest(context))
                    {
                        _logger.LogWarning("Payment security validation failed for request: {Method} {Path} from IP: {IP}", 
                            requestMethod, requestPath, GetClientIpAddress(context));
                        
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Access denied: Payment security validation failed");
                        return;
                    }

                    // Log request details for audit purposes
                    await LogPaymentRequest(context, requestPath, requestMethod);

                    // Validate request body for payment operations
                    if (requestMethod == "POST" || requestMethod == "PUT")
                    {
                        if (!await ValidatePaymentRequestBody(context))
                        {
                            _logger.LogWarning("Payment request body validation failed for: {Method} {Path} from IP: {IP}", 
                                requestMethod, requestPath, GetClientIpAddress(context));
                            
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsync("Invalid payment request data");
                            return;
                        }
                    }

                    _logger.LogInformation("Payment security validation passed for request: {Method} {Path}", 
                        requestMethod, requestPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in payment security middleware for request: {Method} {Path}", 
                        requestMethod, requestPath);
                    
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync("Internal server error during security validation");
                    return;
                }
            }

            // Continue with the request pipeline
            await _next(context);

            // Log response time for payment requests
            if (IsPaymentSecurityEndpoint(requestPath, requestMethod))
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation("Payment request completed: {Method} {Path} in {ProcessingTime}ms", 
                    requestMethod, requestPath, processingTime);
            }
        }

        /// <summary>
        /// Determines if the endpoint requires payment security validation
        /// </summary>
        private bool IsPaymentSecurityEndpoint(string? requestPath, string requestMethod)
        {
            if (string.IsNullOrEmpty(requestPath))
                return false;

            // Payment endpoints that require security validation
            var secureEndpoints = new[]
            {
                "/api/payment/refund",
                "/api/payment/cancel",
                "/api/payment/modify",
                "/api/payment/update-status",
                "/api/payment/void",
                "/api/payment/reverse"
            };

            return Array.Exists(secureEndpoints, endpoint => 
                requestPath.EndsWith(endpoint, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates payment request headers and authentication
        /// </summary>
        private async Task<bool> ValidatePaymentRequest(HttpContext context)
        {
            try
            {
                // Check if user is authenticated
                if (!context.User.Identity?.IsAuthenticated ?? true)
                {
                    _logger.LogWarning("Unauthenticated payment request attempt from IP: {IP}", 
                        GetClientIpAddress(context));
                    return false;
                }

                // Check if user has required role for payment operations
                var userRole = context.User.FindFirst("role")?.Value;
                if (string.IsNullOrEmpty(userRole))
                {
                    _logger.LogWarning("User without role attempting payment operation from IP: {IP}", 
                        GetClientIpAddress(context));
                    return false;
                }

                // Validate user role permissions
                var allowedRoles = new[] { "Administrator", "Manager", "Cashier" };
                if (!Array.Exists(allowedRoles, role => 
                    string.Equals(role, userRole, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("User with insufficient role {UserRole} attempting payment operation from IP: {IP}", 
                        userRole, GetClientIpAddress(context));
                    return false;
                }

                // Check for required headers
                if (!context.Request.Headers.ContainsKey("User-Agent"))
                {
                    _logger.LogWarning("Payment request missing User-Agent header from IP: {IP}", 
                        GetClientIpAddress(context));
                    return false;
                }

                // Validate request timestamp (prevent replay attacks)
                if (context.Request.Headers.ContainsKey("X-Request-Timestamp"))
                {
                    if (!ValidateRequestTimestamp(context.Request.Headers["X-Request-Timestamp"]))
                    {
                        _logger.LogWarning("Invalid request timestamp in payment request from IP: {IP}", 
                            GetClientIpAddress(context));
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating payment request from IP: {IP}", 
                    GetClientIpAddress(context));
                return false;
            }
        }

        /// <summary>
        /// Validates payment request body for security and data integrity
        /// </summary>
        private async Task<bool> ValidatePaymentRequestBody(HttpContext context)
        {
            try
            {
                // Enable request body reading
                context.Request.EnableBuffering();

                // Read request body
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, 
                    detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                
                var requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset position for downstream middleware

                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body in payment request from IP: {IP}", 
                        GetClientIpAddress(context));
                    return false;
                }

                // Validate JSON structure
                try
                {
                    var jsonDocument = JsonDocument.Parse(requestBody);
                    
                    // Check for required fields based on endpoint
                    var requestPath = context.Request.Path.Value?.ToLower();
                    
                    if (requestPath?.Contains("/refund") == true)
                    {
                        if (!ValidateRefundRequest(jsonDocument))
                        {
                            _logger.LogWarning("Invalid refund request structure from IP: {IP}", 
                                GetClientIpAddress(context));
                            return false;
                        }
                    }
                    else if (requestPath?.Contains("/cancel") == true)
                    {
                        if (!ValidateCancelRequest(jsonDocument))
                        {
                            _logger.LogWarning("Invalid cancel request structure from IP: {IP}", 
                                GetClientIpAddress(context));
                            return false;
                        }
                    }
                    else if (requestPath?.Contains("/modify") == true)
                    {
                        if (!ValidateModifyRequest(jsonDocument))
                        {
                            _logger.LogWarning("Invalid modify request structure from IP: {IP}", 
                                GetClientIpAddress(context));
                            return false;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Invalid JSON in payment request from IP: {IP}: {Error}", 
                        GetClientIpAddress(context), ex.Message);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating payment request body from IP: {IP}", 
                    GetClientIpAddress(context));
                return false;
            }
        }

        /// <summary>
        /// Validates refund request structure
        /// </summary>
        private bool ValidateRefundRequest(JsonDocument jsonDocument)
        {
            var root = jsonDocument.RootElement;
            
            // Check required fields for refund
            return root.TryGetProperty("paymentId", out _) &&
                   root.TryGetProperty("refundAmount", out var amountElement) &&
                   amountElement.TryGetDecimal(out var amount) &&
                   amount > 0 &&
                   root.TryGetProperty("refundReason", out var reasonElement) &&
                   !string.IsNullOrWhiteSpace(reasonElement.GetString());
        }

        /// <summary>
        /// Validates cancel request structure
        /// </summary>
        private bool ValidateCancelRequest(JsonDocument jsonDocument)
        {
            var root = jsonDocument.RootElement;
            
            // Check required fields for cancellation
            return root.TryGetProperty("paymentId", out _) &&
                   root.TryGetProperty("cancelReason", out var reasonElement) &&
                   !string.IsNullOrWhiteSpace(reasonElement.GetString());
        }

        /// <summary>
        /// Validates modify request structure
        /// </summary>
        private bool ValidateModifyRequest(JsonDocument jsonDocument)
        {
            var root = jsonDocument.RootElement;
            
            // Check required fields for modification
            return root.TryGetProperty("paymentId", out _) &&
                   root.TryGetProperty("modificationType", out _) &&
                   root.TryGetProperty("newValue", out _);
        }

        /// <summary>
        /// Validates request timestamp to prevent replay attacks
        /// </summary>
        private bool ValidateRequestTimestamp(string timestamp)
        {
            try
            {
                if (DateTime.TryParse(timestamp, out var requestTime))
                {
                    var currentTime = DateTime.UtcNow;
                    var timeDifference = Math.Abs((currentTime - requestTime).TotalMinutes);
                    
                    // Allow requests within 5 minutes of current time
                    return timeDifference <= 5;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Logs payment request details for audit purposes
        /// </summary>
        private async Task LogPaymentRequest(HttpContext context, string requestPath, string requestMethod)
        {
            try
            {
                var userId = context.User.FindFirst("nameid")?.Value ?? "Unknown";
                var userRole = context.User.FindFirst("role")?.Value ?? "Unknown";
                var ipAddress = GetClientIpAddress(context);
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                var timestamp = DateTime.UtcNow;

                var logData = new
                {
                    Timestamp = timestamp,
                    UserId = userId,
                    UserRole = userRole,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    RequestMethod = requestMethod,
                    RequestPath = requestPath,
                    RequestId = context.TraceIdentifier
                };

                _logger.LogInformation("Payment request logged: {@LogData}", logData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging payment request");
            }
        }

        /// <summary>
        /// Gets client IP address from request
        /// </summary>
        private string GetClientIpAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? 
                   context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? 
                   context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? 
                   "Unknown";
        }
    }
}
