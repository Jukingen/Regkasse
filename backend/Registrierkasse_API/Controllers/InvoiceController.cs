using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceController> _logger;
        private readonly ITseService _tseService;

        public InvoiceController(AppDbContext context, ILogger<InvoiceController> logger, ITseService tseService)
        {
            _context = context;
            _logger = logger;
            _tseService = tseService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices()
        {
            return await _context.Invoices
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(Guid id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return invoice;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Cashier")]
        public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] Invoice invoice)
        {
            invoice.Id = Guid.NewGuid();
            invoice.CreatedAt = DateTime.UtcNow;
            invoice.ReceiptNumber = await GenerateReceiptNumber();

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] Invoice invoice)
        {
            if (id != invoice.Id)
            {
                return BadRequest();
            }

            _context.Entry(invoice).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InvoiceExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InvoiceExists(Guid id)
        {
            return _context.Invoices.Any(e => e.Id == id);
        }

        private async Task<string> GenerateReceiptNumber()
        {
            var tseId = await _tseService.GetTseIdAsync();
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            
            var lastReceipt = _context.Invoices
                .Where(i => i.ReceiptNumber.StartsWith($"AT-{tseId}-{date}"))
                .OrderByDescending(i => i.ReceiptNumber)
                .FirstOrDefault();

            int sequence = 1;
            if (lastReceipt != null)
            {
                var lastSeq = lastReceipt.ReceiptNumber.Split('-').Last();
                if (int.TryParse(lastSeq, out int lastSequence))
                {
                    sequence = lastSequence + 1;
                }
            }

            return $"AT-{tseId}-{date}-{sequence:D4}";
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateInvoiceStatus(int id, [FromBody] UpdateInvoiceStatusModel model)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                invoice.Status = model.Status.ToString();
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Fatura durumu güncellendi: #{id} - {model.Status}");

                return Ok(new { message = "Fatura durumu başarıyla güncellendi", invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan fatura durumu güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Fatura durumu güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var yearStart = new DateTime(today.Year, 1, 1);

                var dailyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate.Date == today)
                    .SumAsync(i => i.TotalAmount);

                var monthlyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate >= monthStart)
                    .SumAsync(i => i.TotalAmount);

                var yearlyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate >= yearStart)
                    .SumAsync(i => i.TotalAmount);

                var topProducts = await _context.InvoiceItems
                    .Include(ii => ii.Product)
                    .GroupBy(ii => ii.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        ProductName = g.First().Product.Name,
                        TotalQuantity = g.Sum(ii => ii.Quantity),
                        TotalAmount = g.Sum(ii => ii.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    message = "İstatistikler başarıyla getirildi",
                    statistics = new
                    {
                        dailyTotal,
                        monthlyTotal,
                        yearlyTotal,
                        topProducts
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "İstatistikler getirilirken bir hata oluştu", error = ex.Message });
            }
        }
    }

    public class CreateInvoiceModel
    {
        public int CustomerId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public int RegisterId { get; set; }
        public decimal DiscountAmount { get; set; }
        public InvoiceItemModel[] Items { get; set; } = Array.Empty<InvoiceItemModel>();
    }

    public class InvoiceItemModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class UpdateInvoiceStatusModel
    {
        public InvoiceStatus Status { get; set; }
    }
} 