using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Registrierkasse.Controllers
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
                    CashRegisterId = register.Id,
                    Type = TransactionType.StartDay.ToString(),
                    Amount = model.StartingBalance,
                    Description = "Başlangıç bakiyesi",
                    BalanceBefore = 0,
                    BalanceAfter = model.StartingBalance
                };

                _context.CashRegisterTransactions.Add(registerTransaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Yeni kasa oluşturuldu: {register.RegisterNumber}");

                return CreatedAtAction(nameof(GetCashRegister), new { id = register.Id },
                    new { message = "Kasa başarıyla oluşturuldu", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa oluşturulurken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("open")]
        public async Task<IActionResult> OpenRegister([FromBody] OpenRegisterModel model)
        {
            var register = await _context.CashRegisters.FindAsync(model.RegisterId);
            if (register == null)
                return NotFound($"Kasa bulunamadı: {model.RegisterId}");

            if (register.Status != RegisterStatus.Closed)
                return BadRequest("Kasa zaten açık");

            var user = await _userManager.FindByIdAsync(model.UserId.ToString());
            if (user == null)
                return NotFound($"Kullanıcı bulunamadı: {model.UserId}");

            register.Status = RegisterStatus.Open;
            register.CurrentUserId = model.UserId.ToString();
            register.LastBalanceUpdate = DateTime.UtcNow;

            var transaction = new CashRegisterTransaction
            {
                CashRegisterId = register.Id,
                Type = TransactionType.StartDay.ToString(),
                Amount = model.StartingAmount,
                BalanceBefore = 0,
                BalanceAfter = model.StartingAmount,
                Description = "Gün başlangıcı",
                UserId = model.UserId.ToString(),
                TSESignature = model.TSESignature,
                TSESignatureCounter = model.TSESignatureCounter,
                TSETime = model.TSETime
            };

            _context.CashRegisterTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(register);
        }

        [HttpPost("close")]
        public async Task<IActionResult> CloseRegister([FromBody] CloseRegisterModel model)
        {
            var register = await _context.CashRegisters.FindAsync(model.RegisterId);
            if (register == null)
                return NotFound($"Kasa bulunamadı: {model.RegisterId}");

            if (register.Status != RegisterStatus.Open)
                return BadRequest("Kasa zaten kapalı");

            var user = await _userManager.FindByIdAsync(model.UserId.ToString());
            if (user == null)
                return NotFound($"Kullanıcı bulunamadı: {model.UserId}");

            register.Status = RegisterStatus.Closed;
            register.CurrentUserId = null;
            register.LastBalanceUpdate = DateTime.UtcNow;
            register.LastClosingDate = DateTime.UtcNow;
            register.LastClosingAmount = model.ClosingAmount;

            var transaction = new CashRegisterTransaction
            {
                CashRegisterId = register.Id,
                Type = TransactionType.EndDay.ToString(),
                Amount = model.ClosingAmount,
                BalanceBefore = register.CurrentBalance,
                BalanceAfter = 0,
                Description = "Gün sonu",
                UserId = model.UserId.ToString(),
                TSESignature = model.TSESignature,
                TSESignatureCounter = model.TSESignatureCounter,
                TSETime = model.TSETime
            };

            _context.CashRegisterTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(register);
        }

        [HttpGet("{id}/transactions")]
        public async Task<IActionResult> GetTransactions(Guid id)
        {
            try
            {
                var transactions = await _context.CashRegisterTransactions
                    .Include(t => t.User)
                    .Where(t => t.CashRegisterId == id)
                    .OrderByDescending(t => t.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Kasa işlemleri başarıyla getirildi", transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasa işlemleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Kasa işlemleri getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/assign")]
        public async Task<IActionResult> AssignUser(Guid id, [FromBody] AssignUserModel model)
        {
            try
            {
                var register = await _context.CashRegisters.FindAsync(id);
                if (register == null)
                {
                    return NotFound(new { message = "Kasa bulunamadı" });
                }

                var user = await _userManager.FindByIdAsync(model.UserId.ToString());
                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı" });
                }

                register.CurrentUserId = model.UserId.ToString();
                await _context.SaveChangesAsync();

                return Ok(new { message = "Kullanıcı başarıyla atandı", register });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan kasaya kullanıcı atanırken bir hata oluştu");
                return StatusCode(500, new { message = "Kullanıcı atanırken bir hata oluştu", error = ex.Message });
            }
        }

        private async Task<string> GenerateRegisterNumber()
        {
            var lastRegister = await _context.CashRegisters
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastRegister != null && lastRegister.RegisterNumber != null)
            {
                if (int.TryParse(lastRegister.RegisterNumber.Replace("REG", ""), out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"REG{nextNumber:D3}";
        }
    }

    public class CreateCashRegisterModel
    {
        public string Location { get; set; }
        public decimal StartingBalance { get; set; }
    }

    public class CloseRegisterModel
    {
        public Guid RegisterId { get; set; }
        public decimal ClosingAmount { get; set; }
        public string TSESignature { get; set; } = string.Empty;
        public long TSESignatureCounter { get; set; }
        public DateTime TSETime { get; set; }
        public Guid UserId { get; set; }
    }

    public class AssignUserModel
    {
        public Guid UserId { get; set; }
    }

    public class OpenRegisterModel
    {
        public Guid RegisterId { get; set; }
        public decimal StartingAmount { get; set; }
        public string TSESignature { get; set; } = string.Empty;
        public long TSESignatureCounter { get; set; }
        public DateTime TSETime { get; set; }
        public Guid UserId { get; set; }
    }
} 