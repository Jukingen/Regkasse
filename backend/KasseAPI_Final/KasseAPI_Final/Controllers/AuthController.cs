using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using KasseAPI_Final.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Email}", model.Email);

                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                {
                    return BadRequest(new { message = "Email ve şifre gerekli" });
                }

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    return BadRequest(new { message = "Kullanıcı bulunamadı" });
                }

                if (!user.IsActive)
                {
                    return BadRequest(new { message = "Hesap aktif değil" });
                }

                var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    return BadRequest(new { message = "Geçersiz şifre" });
                }

                var token = GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);

                var response = new
                {
                    token = token,
                    expiresIn = 3600,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        role = user.Role,
                        roles = roles
                    }
                };

                _logger.LogInformation("Login successful for user: {Email}", model.Email);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Email}", model.Email);
                return StatusCode(500, new { message = "Giriş işlemi sırasında hata oluştu" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Logout requested for user: {UserId}", userId);

                // 🧹 KULLANICI SEPETLERİNİ TEMİZLE
                try
                {
                    // CartLifecycleService'i IServiceProvider üzerinden al
                    var cartLifecycleService = HttpContext.RequestServices.GetRequiredService<CartLifecycleService>();
                    await cartLifecycleService.CleanupUserCarts(userId);
                    _logger.LogInformation("User carts cleanup completed for user: {UserId}", userId);
                }
                catch (Exception cartCleanupEx)
                {
                    // Cart cleanup hatası logout'u engellemesin
                    _logger.LogWarning(cartCleanupEx, "Cart cleanup failed for user: {UserId}, but logout will continue", userId);
                }

                _logger.LogInformation("Logout successful for user: {UserId}", userId);
                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                _logger.LogError(ex, "Logout error for user: {UserId}. Exception: {ExceptionType}, Message: {ExceptionMessage}", 
                    userId, ex.GetType().Name, ex.Message);
                
                // Daha detaylı hata mesajı
                var errorMessage = ex.InnerException != null 
                    ? $"Logout error: {ex.Message} - Inner: {ex.InnerException.Message}"
                    : $"Logout error: {ex.Message}";
                
                return StatusCode(500, new { message = errorMessage });
            }
        }

        // 🔐 GET CURRENT USER - F5 refresh'te kullanıcı durumunu kontrol eder
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                _logger.LogInformation("GetCurrentUser endpoint called");
                
                // JWT token'dan user ID'yi al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetCurrentUser: User ID not found in token claims");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                _logger.LogInformation("GetCurrentUser: User ID from token: {UserId}", userId);

                // Kullanıcıyı veritabanından bul
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("GetCurrentUser: User not found in database for ID: {UserId}", userId);
                    return NotFound(new { message = "User not found" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("GetCurrentUser: User account is not active for ID: {UserId}", userId);
                    return BadRequest(new { message = "User account is not active" });
                }

                // Kullanıcı rollerini al
                var roles = await _userManager.GetRolesAsync(user);

                var userResponse = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = user.Role,
                    roles = roles
                };

                _logger.LogInformation("GetCurrentUser: Successfully retrieved user {Email} with role {Role}", user.Email, user.Role);
                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrentUser error. Exception: {ExceptionType}, Message: {ExceptionMessage}", 
                    ex.GetType().Name, ex.Message);
                
                return StatusCode(500, new { message = "Error retrieving user information" });
            }
        }

        // 🔄 REFRESH TOKEN - Token süresi dolduğunda yenileme
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenModel model)
        {
            try
            {
                _logger.LogInformation("Refresh token request received");

                if (string.IsNullOrWhiteSpace(model.RefreshToken))
                {
                    return BadRequest(new { message = "Refresh token is required" });
                }

                // TODO: Refresh token validation logic will be implemented here
                // For now, return a simple response
                _logger.LogWarning("Refresh token endpoint not fully implemented yet");
                return BadRequest(new { message = "Refresh token functionality not implemented yet" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token error. Exception: {ExceptionType}, Message: {ExceptionMessage}", 
                    ex.GetType().Name, ex.Message);
                
                return StatusCode(500, new { message = "Error during token refresh" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    EmployeeNumber = model.EmployeeNumber,
                    IsActive = true
                };
                
                var result = await _userManager.CreateAsync(user, model.Password);
                
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Cashier");
                    return Ok(new { message = "Kullanıcı başarıyla oluşturuldu" });
                }
                
                return BadRequest(new { errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                return StatusCode(500, "Internal server error");
            }
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"] ?? "default-secret-key-32-chars-long"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddHours(1);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("user_id", user.Id),
                    new Claim("user_role", user.Role ?? "User"),
                    // ASP.NET Core role-based authorization bu claim'i bekler
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                },
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
    }

    public class RefreshTokenModel
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
