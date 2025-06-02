using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoryController(AppDbContext context, ILogger<InventoryController> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inventory>>> GetInventory()
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Inventory>> GetInventoryItem(Guid id)
        {
            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null)
            {
                return NotFound();
            }

            return inventory;
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateInventory(Guid id, [FromBody] Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return BadRequest();
            }

            _context.Entry(inventory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<Inventory>> CreateInventory([FromBody] Inventory inventory)
        {
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventoryItem), new { id = inventory.Id }, inventory);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteInventory(Guid id)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null)
            {
                return NotFound();
            }

            _context.Inventories.Remove(inventory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InventoryExists(Guid id)
        {
            return _context.Inventories.Any(e => e.Id == id);
        }

        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockItems()
        {
            try
            {
                var lowStockItems = await _context.Inventories
                    .Include(i => i.Product)
                    .Where(i => i.CurrentStock <= i.MinimumStock)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Düşük stoklu ürünler başarıyla getirildi", lowStockItems });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Düşük stoklu ürünler getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Düşük stoklu ürünler getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("adjust")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentModel model)
        {
            try
            {
                var inventory = await _context.Inventories
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

                if (inventory == null)
                {
                    return NotFound(new { message = "Ürün stok kaydı bulunamadı" });
                }

                var oldStock = inventory.CurrentStock;
                inventory.CurrentStock = model.NewStock;
                inventory.LastStockUpdate = DateTime.UtcNow;

                var transaction = new InventoryTransaction
                {
                    InventoryId = inventory.Id,
                    Type = model.Type.ToString(),
                    Quantity = model.NewStock - oldStock,
                    Reference = model.Reference,
                    Notes = model.Notes,
                    UserId = model.UserId
                };

                _context.InventoryTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Stok güncellendi - Ürün: {inventory.Product.Name}, Eski: {oldStock}, Yeni: {model.NewStock}");

                return Ok(new { message = "Stok başarıyla güncellendi", inventory });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Stok güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("receive")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> ReceiveStock([FromBody] StockReceiveModel model)
        {
            try
            {
                var inventory = await _context.Inventories
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

                if (inventory == null)
                {
                    return NotFound(new { message = "Ürün stok kaydı bulunamadı" });
                }

                inventory.CurrentStock += model.Quantity;
                inventory.LastStockUpdate = DateTime.UtcNow;

                var transaction = new InventoryTransaction
                {
                    InventoryId = inventory.Id,
                    Type = model.Type.ToString(),
                    Quantity = model.Quantity,
                    Reference = model.Reference,
                    Notes = model.Notes,
                    UserId = model.UserId
                };

                _context.InventoryTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Stok alındı - Ürün: {inventory.Product.Name}, Miktar: {model.Quantity}");

                return Ok(new { message = "Stok başarıyla alındı", inventory });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok alınırken bir hata oluştu");
                return StatusCode(500, new { message = "Stok alınırken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var transactions = await _context.InventoryTransactions
                    .Include(t => t.Inventory)
                    .ThenInclude(i => i.Product)
                    .OrderByDescending(t => t.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Stok işlemleri başarıyla getirildi", transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok işlemleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Stok işlemleri getirilirken bir hata oluştu", error = ex.Message });
            }
        }
    }

    public class StockAdjustmentModel
    {
        public Guid ProductId { get; set; }
        public decimal NewStock { get; set; }
        public string Reference { get; set; }
        public string Notes { get; set; }
        public InventoryTransactionType Type { get; set; }
        public string UserId { get; set; }
    }

    public class CreateInventoryTransactionModel
    {
        public Guid InventoryId { get; set; }
        public InventoryTransactionType Type { get; set; }
        public decimal Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    public class UpdateInventoryStockModel
    {
        public decimal NewStock { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public InventoryTransactionType Type { get; set; }
        public string UserId { get; set; } = string.Empty;
    }

    public class RestockInventoryModel
    {
        public decimal Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public InventoryTransactionType Type { get; set; }
        public string UserId { get; set; } = string.Empty;
    }

    public class StockReceiveModel
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public InventoryTransactionType Type { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
} 