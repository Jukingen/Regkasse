using Microsoft.IdentityModel.Tokens;
using Registrierkasse_API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Registrierkasse_API.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;
        private readonly RoleService _roleService;
        private readonly ILogger<JwtService> _logger;

        public JwtService(IConfiguration configuration, RoleService roleService, ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _roleService = roleService;
            _logger = logger;
        }

        public async Task<string> GenerateTokenAsync(ApplicationUser user)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"] ?? "default-secret-key");

                // Kullanıcının rollerini getir
                var userRoles = await _roleService.GetUserRolesAsync(user.Id);
                var userPermissions = await _roleService.GetUserPermissionsAsync(user.Id);
                var isDemoUser = await _roleService.IsDemoUserAsync(user.Id);

                // Claims oluştur
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim("FirstName", user.FirstName),
                    new Claim("LastName", user.LastName),
                    new Claim("EmployeeNumber", user.EmployeeNumber),
                    new Claim("IsDemo", isDemoUser.ToString()),
                    new Claim("AccountType", user.AccountType),
                    new Claim("LoginCount", user.LoginCount.ToString())
                };

                // Rolleri claims'e ekle
                foreach (var role in userRoles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                // Yetkileri claims'e ekle
                foreach (var permission in userPermissions)
                {
                    claims.Add(new Claim("Permission", permission));
                }

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddHours(8), // 8 saat geçerli
                    Issuer = _configuration["JwtSettings:Issuer"],
                    Audience = _configuration["JwtSettings:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                // Giriş istatistiklerini güncelle
                await _roleService.UpdateLoginStatsAsync(user.Id);

                _logger.LogInformation($"JWT token generated for user {user.UserName} with roles: {string.Join(", ", userRoles)}");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating JWT token for user {user.UserName}");
                throw;
            }
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"] ?? "default-secret-key");

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token");
                return null;
            }
        }

        public string? GetUserIdFromToken(string token)
        {
            try
            {
                var principal = ValidateToken(token);
                return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID from token");
                return null;
            }
        }

        public List<string> GetRolesFromToken(string token)
        {
            try
            {
                var principal = ValidateToken(token);
                return principal?.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles from token");
                return new List<string>();
            }
        }

        public List<string> GetPermissionsFromToken(string token)
        {
            try
            {
                var principal = ValidateToken(token);
                return principal?.FindAll("Permission")
                    .Select(c => c.Value)
                    .ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions from token");
                return new List<string>();
            }
        }

        public bool IsDemoUserFromToken(string token)
        {
            try
            {
                var principal = ValidateToken(token);
                var isDemoClaim = principal?.FindFirst("IsDemo")?.Value;
                return bool.TryParse(isDemoClaim, out var isDemo) && isDemo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user is demo from token");
                return false;
            }
        }
    }
} 