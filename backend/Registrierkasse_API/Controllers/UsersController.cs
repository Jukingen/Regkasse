using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Registrierkasse_API.Models;
using Registrierkasse_API.Data;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                var roles = await _userManager.GetRolesAsync(user);

                var profile = new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    employeeNumber = user.EmployeeNumber,
                    role = user.Role,
                    emailConfirmed = user.EmailConfirmed,
                    isActive = user.IsActive,
                    createdAt = DateTime.UtcNow.AddDays(-30), // Demo için
                    lastLoginAt = user.LastLogin ?? DateTime.UtcNow,
                    twoFactorEnabled = user.TwoFactorEnabled,
                    roles = roles
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve user profile", details = ex.Message });
            }
        }
    }
} 
