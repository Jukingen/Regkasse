using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Services;
using KasseAPI_Final.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly ITokenClaimsService _tokenClaimsService;
        private readonly AuthOptions _authOptions;
        private readonly IRefreshTokenService _refreshTokenService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            ITokenClaimsService tokenClaimsService,
            IOptions<AuthOptions> authOptions,
            IRefreshTokenService refreshTokenService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _tokenClaimsService = tokenClaimsService;
            _authOptions = authOptions.Value;
            _refreshTokenService = refreshTokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Email}, clientApp: {ClientApp}", model.Email, model.ClientApp ?? "(none)");

                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                {
                    return BadRequest(new { message = "Email ve şifre gerekli" });
                }

                // --- clientApp validation (fail-closed) ---
                var allowLegacy = _authOptions.AllowLegacyLoginWithoutClientApp;

                string? resolvedClientApp = model.ClientApp?.Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(resolvedClientApp))
                {
                    if (!allowLegacy)
                    {
                        return BadRequest(new { message = "clientApp field is required (\"pos\" or \"admin\")." });
                    }

                    _logger.LogWarning("Login without clientApp from {Email}. AllowLegacyLoginWithoutClientApp is ON — legacy mode.", model.Email);
                    resolvedClientApp = null;
                }
                else if (!ClientAppPolicy.IsKnownApp(resolvedClientApp))
                {
                    return BadRequest(new { message = $"Unknown clientApp value: \"{model.ClientApp}\". Allowed: pos, admin." });
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

                // --- Role-level app policy check ---
                var roles = await _userManager.GetRolesAsync(user);
                var primaryRole = roles.FirstOrDefault() ?? user.Role ?? Roles.FallbackUnknown;
                var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);

                if (resolvedClientApp != null && !ClientAppPolicy.IsRoleAllowedForApp(resolvedClientApp, roles.Append(primaryRole)))
                {
                    _logger.LogWarning(
                        "Login denied: user {Email} (role {Role}) is not allowed for clientApp {ClientApp}",
                        model.Email, canonicalRole, resolvedClientApp);

                    return StatusCode(403, new { message = "Bu kullanıcı bu uygulama için yetkili değil." });
                }

                // Persist last login for audit and UI (Users list / detail).
                user.LastLoginAt = DateTime.UtcNow;
                user.LoginCount++;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning("LastLoginAt/LoginCount update failed for user {UserId}, login will continue. Errors: {Errors}",
                        user.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                }

                var issuedTokens = await _refreshTokenService.IssueLoginTokensAsync(
                    user.Id,
                    resolvedClientApp ?? "legacy",
                    async (tokenUserId, jti, sessionId, expiresAtUtc, clientApp) =>
                    {
                        var claims = await _tokenClaimsService.BuildClaimsAsync(user, roles, appContext: resolvedClientApp);
                        return GenerateJwtToken(claims, jti, sessionId, expiresAtUtc);
                    });
                var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles).ToList();

                var response = new
                {
                    token = issuedTokens.AccessToken,
                    expiresIn = Math.Max(60, _authOptions.AccessTokenLifetimeMinutes * 60),
                    refreshToken = issuedTokens.RefreshToken,
                    refreshTokenExpiresAtUtc = issuedTokens.RefreshTokenExpiresAtUtc,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        role = canonicalRole,
                        roles = roles,
                        permissions = permissions,
                        isDemo = user.IsDemo
                    },
                    appContext = resolvedClientApp
                };

                _logger.LogInformation("Login successful for user: {Email}, clientApp: {ClientApp}", model.Email, resolvedClientApp ?? "legacy");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Email}", model.Email);
                return StatusCode(500, new { message = "Giriş işlemi sırasında hata oluştu" });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Logout requested for user: {UserId}", userId);
                var sidRaw = User.FindFirst("sid")?.Value;
                if (Guid.TryParse(sidRaw, out var sessionId))
                {
                    await _refreshTokenService.LogoutSessionAsync(sessionId, "logout");
                }

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

                // Kullanıcı rollerini al; permission set RolePermissionMatrix'ten
                var roles = await _userManager.GetRolesAsync(user);
                var primaryRole = roles.FirstOrDefault() ?? user.Role ?? Roles.FallbackUnknown;
                var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles).ToList();
                var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);

                var appContext = User.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;

                var userResponse = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = canonicalRole,
                    roles = roles,
                    permissions = permissions,
                    isDemo = user.IsDemo,
                    appContext = appContext
                };

                _logger.LogInformation("GetCurrentUser: Successfully retrieved user {Email} with role {Role}, appContext {AppContext}", user.Email, user.Role, appContext ?? "none");
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

                var result = await _refreshTokenService.RotateAsync(
                    model.RefreshToken,
                    async (tokenUserId, jti, sessionId, expiresAtUtc, clientApp) =>
                    {
                        var user = await _userManager.FindByIdAsync(tokenUserId);
                        if (user == null)
                            throw new InvalidOperationException("Refresh token user not found");
                        var roles = await _userManager.GetRolesAsync(user);
                        var claims = await _tokenClaimsService.BuildClaimsAsync(user, roles, appContext: clientApp);
                        return GenerateJwtToken(claims, jti, sessionId, expiresAtUtc);
                    });

                if (!result.Success || result.Tokens == null)
                {
                    var code = result.ReuseDetected ? "refresh_token_reuse_detected" : result.ErrorCode;
                    return Unauthorized(new { message = "Refresh failed", code });
                }

                return Ok(new
                {
                    token = result.Tokens.AccessToken,
                    expiresIn = Math.Max(60, _authOptions.AccessTokenLifetimeMinutes * 60),
                    refreshToken = result.Tokens.RefreshToken,
                    refreshTokenExpiresAtUtc = result.Tokens.RefreshTokenExpiresAtUtc
                });
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
                    await _userManager.AddToRoleAsync(user, Roles.Cashier);
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

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "User not authenticated" });

            await _refreshTokenService.LogoutAllAsync(userId, "logout_all");
            return Ok(new { message = "All sessions invalidated" });
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var revoked = await _refreshTokenService.RevokeRefreshTokenAsync(model.RefreshToken, "manual_revoke");
            if (!revoked)
                return NotFound(new { message = "Refresh token not found" });
            return Ok(new { message = "Refresh token revoked" });
        }

        private string GenerateJwtToken(IReadOnlyList<Claim> baseClaims, string jti, Guid sessionId, DateTime expiresAtUtc)
        {
            var secretKey = _configuration["JwtSettings:SecretKey"]!;
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = baseClaims.ToList();
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
            claims.Add(new Claim("sid", sessionId.ToString()));

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Target client application: "pos" or "admin".
        /// When AllowLegacyLoginWithoutClientApp is false (default), this field is required.
        /// </summary>
        public string? ClientApp { get; set; }
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
