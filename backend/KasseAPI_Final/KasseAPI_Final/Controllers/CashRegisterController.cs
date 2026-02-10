using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CashRegisterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CashRegisterController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public CashRegisterController(
            ILogger<CashRegisterController> logger,
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetCashRegisters()
        {
            try
            {
                var registers = await _context.CashRegisters
                    .Include(cr => cr.CurrentUser)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Kasalar başarıyla getirildi", registers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasalar getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Kasalar getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCashRegister(Guid id)
        {
            try
            {
                var register = await _context.CashRegisters
                    .Include(r => r.CurrentUser)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (register == null)
                {
                    return NotFound(new { message = "Kasa bulunamadı" });
                }

                return Ok(new { message = "Kasa başarıyla getirildi", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CreateCashRegister([FromBody] CreateCashRegisterModel model)
        {
            try
            {
                var register = new CashRegister
                {
                    RegisterNumber = await GenerateRegisterNumber(),
                    Location = model.Location,
                    StartingBalance = model.StartingBalance,
                    CurrentBalance = model.StartingBalance,
                    LastBalanceUpdate = DateTime.UtcNow,
                    Status = RegisterStatus.Closed
                };

                _context.CashRegisters.Add(register);
                await _context.SaveChangesAsync();

                var registerTransaction = new CashRegisterTransaction
                {
                    CashRegisterId = register.Id.ToString(),
                    TransactionType = TransactionType.Open,
                    Amount = model.StartingBalance,
                    Description = "Başlangıç bakiyesi",
                    UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    TransactionDate = DateTime.UtcNow
                };

                _context.CashRegisterTransactions.Add(registerTransaction);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCashRegister), new { id = register.Id }, 
                    new { message = "Kasa başarıyla oluşturuldu", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa oluşturulurken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/open")]
        public async Task<IActionResult> OpenCashRegister(Guid id, [FromBody] OpenCashRegisterModel model)
        {
            try
            {
                var register = await _context.CashRegisters.FindAsync(id);
                if (register == null)
                {
                    return NotFound(new { message = "Kasa bulunamadı" });
                }

                if (register.Status == RegisterStatus.Open)
                {
                    return BadRequest(new { message = "Kasa zaten açık" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });
                }

                register.Status = RegisterStatus.Open;
                register.CurrentUser = user;
                register.LastBalanceUpdate = DateTime.UtcNow;
                register.UpdatedAt = DateTime.UtcNow;

                var transaction = new CashRegisterTransaction
                {
                    CashRegisterId = register.Id.ToString(),
                    TransactionType = TransactionType.Open,
                    Amount = model.OpeningBalance,
                    Description = "Kasa açılışı",
                    UserId = userId,
                    TransactionDate = DateTime.UtcNow
                };

                _context.CashRegisterTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Kasa başarıyla açıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa açılırken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa açılırken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/close")]
        public async Task<IActionResult> CloseCashRegister(Guid id, [FromBody] CloseCashRegisterModel model)
        {
            try
            {
                var register = await _context.CashRegisters.FindAsync(id);
                if (register == null)
                {
                    return NotFound(new { message = "Kasa bulunamadı" });
                }

                if (register.Status == RegisterStatus.Closed)
                {
                    return BadRequest(new { message = "Kasa zaten kapalı" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (register.CurrentUser?.Id != userId)
                {
                    return Forbid();
                }

                register.Status = RegisterStatus.Closed;
                register.CurrentBalance = model.ClosingBalance;
                register.LastBalanceUpdate = DateTime.UtcNow;
                register.UpdatedAt = DateTime.UtcNow;
                register.CurrentUser = null;

                var transaction = new CashRegisterTransaction
                {
                    CashRegisterId = register.Id.ToString(),
                    TransactionType = TransactionType.Close,
                    Amount = model.ClosingBalance,
                    Description = "Kasa kapanışı",
                    UserId = userId,
                    TransactionDate = DateTime.UtcNow
                };

                _context.CashRegisterTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Kasa başarıyla kapatıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa kapatılırken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa kapatılırken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}/transactions")]
        public async Task<IActionResult> GetCashRegisterTransactions(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.CashRegisterTransactions
                    .Where(t => t.CashRegisterId == id.ToString());

                if (startDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate <= endDate.Value);
                }

                var transactions = await query
                    .Include(t => t.User)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                return Ok(new { message = "İşlemler başarıyla getirildi", transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasanın işlemleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "İşlemler getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        private async Task<string> GenerateRegisterNumber()
        {
            var lastRegister = await _context.CashRegisters
                .OrderByDescending(r => r.RegisterNumber)
                .FirstOrDefaultAsync();

            if (lastRegister == null)
            {
                return "K001";
            }

            var lastNumber = int.Parse(lastRegister.RegisterNumber.Substring(1));
            return $"K{(lastNumber + 1):D3}";
        }
    }

    // DTOs
    public class CreateCashRegisterModel
    {
        [Required]
        public string Location { get; set; } = string.Empty;
        
        [Required]
        public decimal StartingBalance { get; set; }
    }

    public class OpenCashRegisterModel
    {
        [Required]
        public decimal OpeningBalance { get; set; }
    }

    public class CloseCashRegisterModel
    {
        [Required]
        public decimal ClosingBalance { get; set; }
    }
}
