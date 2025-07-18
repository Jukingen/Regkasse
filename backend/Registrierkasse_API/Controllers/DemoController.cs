using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Services;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController : ControllerBase
    {
        private readonly DemoUserService _demoUserService;
        private readonly ILogger<DemoController> _logger;

        public DemoController(DemoUserService demoUserService, ILogger<DemoController> logger)
        {
            _demoUserService = demoUserService;
            _logger = logger;
        }

        [HttpPost("create-users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDemoUsers()
        {
            try
            {
                await _demoUserService.CreateDemoUsersAsync();
                return Ok(new { message = "Demo kullanıcılar başarıyla oluşturuldu" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı oluşturma hatası");
                return StatusCode(500, new { error = "Demo kullanıcılar oluşturulamadı" });
            }
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDemoUsers()
        {
            try
            {
                var users = await _demoUserService.GetDemoUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı listesi alınamadı");
                return StatusCode(500, new { error = "Kullanıcı listesi alınamadı" });
            }
        }

        [HttpDelete("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDemoUsers()
        {
            try
            {
                await _demoUserService.DeleteDemoUsersAsync();
                return Ok(new { message = "Demo kullanıcılar başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo kullanıcı silme hatası");
                return StatusCode(500, new { error = "Demo kullanıcılar silinemedi" });
            }
        }

        [HttpGet("login-info")]
        public IActionResult GetDemoLoginInfo()
        {
            var loginInfo = new
            {
                Cashiers = new[]
                {
                    new { Username = "demo.cashier1", Password = "Demo123!", Role = "Cashier", Description = "Demo Kasiyer 1", Permissions = new[] { "Satış", "Sepet", "Ödeme", "Fatura" } },
                    new { Username = "demo.cashier2", Password = "Demo123!", Role = "Cashier", Description = "Demo Kasiyer 2", Permissions = new[] { "Satış", "Sepet", "Ödeme", "Fatura" } }
                },
                Admins = new[]
                {
                    new { Username = "demo.admin1", Password = "Admin123!", Role = "Admin", Description = "Demo Admin 1", Permissions = new[] { "Tüm özellikler" } },
                    new { Username = "demo.admin2", Password = "Admin123!", Role = "Admin", Description = "Demo Admin 2", Permissions = new[] { "Tüm özellikler" } }
                }
            };

            return Ok(loginInfo);
        }
    }
} 