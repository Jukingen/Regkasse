using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CashRegistersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CashRegistersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCashRegisters()
        {
            try
            {
                var cashRegisters = await _context.CashRegisters
                    .Select(cr => new
                    {
                        id = cr.Id,
                        registerNumber = cr.RegisterNumber,
                        tseId = cr.TseId,
                        kassenId = cr.KassenId,
                        location = cr.Location,
                        startingBalance = cr.StartingBalance,
                        currentBalance = cr.CurrentBalance,
                        lastBalanceUpdate = cr.LastBalanceUpdate,
                        status = cr.Status.ToString()
                    })
                    .ToListAsync();

                return Ok(cashRegisters);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve cash registers", details = ex.Message });
            }
        }
    }
} 