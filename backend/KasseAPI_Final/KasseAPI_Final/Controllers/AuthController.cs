using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using KasseAPI_Final.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;

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
                issuer: null,
                audience: null,
                claims: new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("user_id", user.Id),
                    new Claim("user_role", user.Role ?? "User")
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
}
