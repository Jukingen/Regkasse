using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Registrierkasse.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Registrierkasse.Services;
using Microsoft.AspNetCore.Authorization;

namespace Registrierkasse.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly ISessionService _sessionService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            ISessionService sessionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _sessionService = sessionService;
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
                    // Varsayılan rolü ata
                    await _userManager.AddToRoleAsync(user, "Cashier");
                    
                    _logger.LogInformation("User created a new account with password.");
                    
                    return Ok(new { message = "Kullanıcı başarıyla oluşturuldu" });
                }
                
                return BadRequest(new { errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Email}", model.Email);

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found - {Email}", model.Email);
                    return BadRequest(new { message = "Geçersiz kullanıcı adı veya şifre" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login failed: User is inactive - {Email}", model.Email);
                    return BadRequest(new { message = "Hesap aktif değil" });
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Password sign in successful for user: {Email}", model.Email);

                    // Son giriş zamanını güncelle
                    user.LastLogin = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Session oluştur
                    var deviceInfo = $"{Request.Headers["User-Agent"]} - {Request.HttpContext.Connection.RemoteIpAddress}";
                    var sessionId = await _sessionService.CreateSessionAsync(user, deviceInfo);

                    // Token üret
                    var token = await GenerateJwtToken(user);
                    var refreshToken = await GenerateRefreshToken(user);

                    _logger.LogInformation("Tokens generated successfully for user: {Email}", model.Email);

                    // Kullanıcı bilgilerini hazırla
                    var userInfo = new
                    {
                        id = user.Id,
                        email = user.Email,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        role = user.Role,
                        employeeNumber = user.EmployeeNumber,
                        sessionId = sessionId
                    };

                    return Ok(new
                    {
                        token,
                        refreshToken,
                        user = userInfo,
                        message = "Giriş başarılı"
                    });
                }
                
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Login failed: Account locked out - {Email}", model.Email);
                    return BadRequest(new { message = "Hesap kilitlendi" });
                }

                _logger.LogWarning("Login failed: Invalid credentials - {Email}", model.Email);
                return BadRequest(new { message = "Geçersiz kullanıcı adı veya şifre" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging in user: {Email}", model.Email);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken(RefreshTokenModel model)
        {
            try
            {
                _logger.LogInformation("Token refresh attempt");

                var principal = GetPrincipalFromExpiredToken(model.Token);
                if (principal == null)
                {
                    _logger.LogWarning("Token refresh failed: Invalid token");
                    return BadRequest(new { message = "Geçersiz token" });
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                
                _logger.LogInformation("RefreshToken - UserId: {UserId}, Email: {Email}", userId, userEmail);
                
                ApplicationUser? user = null;
                
                // Önce ID ile dene
                if (!string.IsNullOrEmpty(userId))
                {
                    user = await _userManager.FindByIdAsync(userId);
                    _logger.LogInformation("User lookup by ID result: {Found}", user != null);
                }
                
                // ID ile bulunamazsa email ile dene
                if (user == null && !string.IsNullOrEmpty(userEmail))
                {
                    user = await _userManager.FindByEmailAsync(userEmail);
                    _logger.LogInformation("User lookup by email result: {Found}", user != null);
                }
                
                if (user == null)
                {
                    _logger.LogWarning("Token refresh failed: User not found - ID: {UserId}, Email: {Email}", userId, userEmail);
                    return BadRequest(new { message = "Kullanıcı bulunamadı" });
                }

                // Yeni token üret
                var newToken = await GenerateJwtToken(user);
                var newRefreshToken = await GenerateRefreshToken(user);

                _logger.LogInformation("Tokens refreshed successfully for user: {Email}", user.Email);

                return Ok(new
                {
                    token = newToken,
                    refreshToken = newRefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var sessionId = Request.Headers["X-Session-Id"].ToString();

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sessionId))
                {
                    await _sessionService.InvalidateSessionAsync(userId, sessionId);
                }

                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out successfully");
                return Ok(new { message = "Çıkış başarılı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out user");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await _sessionService.InvalidateAllSessionsAsync(userId);
                }

                await _signInManager.SignOutAsync();
                _logger.LogInformation("All sessions invalidated for user");
                return Ok(new { message = "Tüm oturumlar sonlandırıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out all sessions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                // Tüm claims'leri logla
                var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
                _logger.LogInformation("All JWT claims: {Claims}", string.Join(", ", allClaims));
                
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                var customUserId = User.FindFirst("user_id")?.Value;
                var customUserEmail = User.FindFirst("user_email")?.Value;
                
                _logger.LogInformation("GetCurrentUser - UserId: {UserId}, Email: {Email}, Name: {Name}, CustomUserId: {CustomUserId}, CustomEmail: {CustomEmail}", 
                    userId, userEmail, userName, customUserId, customUserEmail);
                
                ApplicationUser? user = null;
                
                // Önce ID ile dene
                if (!string.IsNullOrEmpty(userId))
                {
                    user = await _userManager.FindByIdAsync(userId);
                    _logger.LogInformation("User lookup by ID result: {Found}", user != null);
                }
                
                // Custom user_id ile dene
                if (user == null && !string.IsNullOrEmpty(customUserId))
                {
                    user = await _userManager.FindByIdAsync(customUserId);
                    _logger.LogInformation("User lookup by custom user_id result: {Found}", user != null);
                }
                
                // Email ile dene
                if (user == null && !string.IsNullOrEmpty(userEmail))
                {
                    user = await _userManager.FindByEmailAsync(userEmail);
                    _logger.LogInformation("User lookup by email result: {Found}", user != null);
                }
                
                // Custom email ile dene
                if (user == null && !string.IsNullOrEmpty(customUserEmail))
                {
                    user = await _userManager.FindByEmailAsync(customUserEmail);
                    _logger.LogInformation("User lookup by custom email result: {Found}", user != null);
                }
                
                // Name claim ile dene (email olabilir)
                if (user == null && !string.IsNullOrEmpty(userName))
                {
                    user = await _userManager.FindByEmailAsync(userName);
                    _logger.LogInformation("User lookup by name claim result: {Found}", user != null);
                }
                
                if (user == null)
                {
                    _logger.LogWarning("GetCurrentUser failed: User not found - ID: {UserId}, Email: {Email}, Name: {Name}, CustomUserId: {CustomUserId}, CustomEmail: {CustomEmail}", 
                        userId, userEmail, userName, customUserId, customUserEmail);
                    return BadRequest(new { message = "Kullanıcı bulunamadı" });
                }

                var userInfo = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = user.Role,
                    employeeNumber = user.EmployeeNumber
                };

                _logger.LogInformation("Current user info retrieved successfully for user: {Email}", user.Email);
                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email), // Email
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id), // User ID
                new Claim(ClaimTypes.Name, user.Email), // Email as name
                new Claim("user_id", user.Id), // Extra user ID claim
                new Claim("user_email", user.Email), // Extra email claim
                new Claim("user_role", user.Role) // Custom claim olarak ekle
            };

            // Identity rolleri ekle
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:ExpirationInMinutes"]));

            var token = new JwtSecurityToken(
                _configuration["JwtSettings:Issuer"],
                _configuration["JwtSettings:Audience"],
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<string> GenerateRefreshToken(ApplicationUser user)
        {
            var refreshToken = Guid.NewGuid().ToString();
            var refreshTokenExpiry = DateTime.UtcNow.AddDays(7); // 7 gün geçerli

            // Refresh token'ı kullanıcıya kaydet
            await _userManager.SetAuthenticationTokenAsync(
                user,
                "Registrierkasse",
                "RefreshToken",
                refreshToken
            );

            return refreshToken;
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"])),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidAudience = _configuration["JwtSettings:Audience"],
                ValidateLifetime = false // Süre kontrolünü devre dışı bırak
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }

    public class RegisterModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        public string EmployeeNumber { get; set; } = string.Empty;
    }

    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Password { get; set; } = string.Empty;
        
        public bool RememberMe { get; set; }
    }

    public class RefreshTokenModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
} 