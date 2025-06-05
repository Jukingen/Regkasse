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

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
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
                _logger.LogInformation("Login attempt for user: {Email}", model.Email); // Debug log

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found - {Email}", model.Email); // Debug log
                    return BadRequest(new { message = "Geçersiz kullanıcı adı veya şifre" });
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login failed: User is inactive - {Email}", model.Email); // Debug log
                    return BadRequest(new { message = "Hesap aktif değil" });
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Password sign in successful for user: {Email}", model.Email); // Debug log

                    // Son giriş zamanını güncelle
                    user.LastLogin = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Token üret
                    var token = await GenerateJwtToken(user);
                    var refreshToken = await GenerateRefreshToken(user);

                    _logger.LogInformation("Tokens generated successfully for user: {Email}", model.Email); // Debug log

                    // Kullanıcı bilgilerini hazırla
                    var userInfo = new
                    {
                        id = user.Id,
                        email = user.Email,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        role = user.Role,
                        employeeNumber = user.EmployeeNumber
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
                    _logger.LogWarning("Login failed: Account locked out - {Email}", model.Email); // Debug log
                    return BadRequest(new { message = "Hesap kilitlendi" });
                }

                _logger.LogWarning("Login failed: Invalid credentials - {Email}", model.Email); // Debug log
                return BadRequest(new { message = "Geçersiz kullanıcı adı veya şifre" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging in user: {Email}", model.Email); // Debug log
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken(RefreshTokenModel model)
        {
            try
            {
                _logger.LogInformation("Token refresh attempt"); // Debug log

                var principal = GetPrincipalFromExpiredToken(model.Token);
                if (principal == null)
                {
                    _logger.LogWarning("Token refresh failed: Invalid token"); // Debug log
                    return BadRequest(new { message = "Geçersiz token" });
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Token refresh failed: User ID not found in token"); // Debug log
                    return BadRequest(new { message = "Geçersiz token" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Token refresh failed: User not found - {UserId}", userId); // Debug log
                    return BadRequest(new { message = "Kullanıcı bulunamadı" });
                }

                // Yeni token üret
                var newToken = await GenerateJwtToken(user);
                var newRefreshToken = await GenerateRefreshToken(user);

                _logger.LogInformation("Tokens refreshed successfully for user: {Email}", user.Email); // Debug log

                return Ok(new
                {
                    token = newToken,
                    refreshToken = newRefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token"); // Debug log
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out successfully"); // Debug log
                return Ok(new { message = "Çıkış başarılı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out user"); // Debug log
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetCurrentUser failed: User ID not found in token"); // Debug log
                    return BadRequest(new { message = "Geçersiz token" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("GetCurrentUser failed: User not found - {UserId}", userId); // Debug log
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

                _logger.LogInformation("Current user info retrieved successfully for user: {Email}", user.Email); // Debug log
                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user"); // Debug log
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // Rolleri ekle
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