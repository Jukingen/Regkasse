using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(AppDbContext context, ILogger<InventoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/inventory
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            try
            {
                var inventory = await _context.Inventory
                    .Where(i => i.IsActive)
                    .ToListAsync();

                // Product bilgilerini ayrı sorgu ile al
                var productIds = inventory.Select(i => i.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

                // Inventory item'ları product bilgileri ile zenginleştir
                var enrichedInventory = inventory.Select(item => {
                    var product = products.TryGetValue(item.ProductId, out var p) ? p : null;
                    return new
                    {
                        item.Id,
                        item.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        ProductCategory = product?.Category ?? "Unknown Category",
                        item.CurrentStock,
                        item.MinStockLevel,
                        item.MaxStockLevel,
                        item.ReorderPoint,
                        item.UnitCost,
                        item.LastRestocked,
                        item.Notes,
                        item.IsActive
                    };
                }).OrderBy(i => i.ProductName).ToList();

                return Ok(enrichedInventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/inventory/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(Guid id)
        {
            try
            {
                var inventoryItem = await _context.Inventory
                    .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

                if (inventoryItem == null)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                if (inventoryItem == null)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                // Product bilgilerini ayrı sorgu ile al
                var product = await _context.Products.FindAsync(inventoryItem.ProductId);
                
                var enrichedItem = new
                {
                    inventoryItem.Id,
                    inventoryItem.ProductId,
                    ProductName = product?.Name ?? "Unknown Product",
                    ProductCategory = product?.Category ?? "Unknown Category",
                    inventoryItem.CurrentStock,
                    inventoryItem.MinStockLevel,
                    inventoryItem.MaxStockLevel,
                    inventoryItem.ReorderPoint,
                    inventoryItem.UnitCost,
                    inventoryItem.LastRestocked,
                    inventoryItem.Notes,
                    inventoryItem.IsActive
                };

                return Ok(enrichedItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/inventory
        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<InventoryItem>> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Ürün kontrol et
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return NotFound(new { message = "Product not found" });
                }

                // Mevcut inventory item kontrol et
                var existingItem = await _context.Inventory
                    .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.IsActive);

                if (existingItem != null)
                {
                    return BadRequest(new { message = "Inventory item already exists for this product" });
                }

                var inventoryItem = new InventoryItem
                {
                    ProductId = request.ProductId,
                    CurrentStock = request.InitialStock,
                    MinStockLevel = request.MinStockLevel,
                    MaxStockLevel = request.MaxStockLevel,
                    ReorderPoint = request.ReorderPoint,
                    UnitCost = request.UnitCost,
                    LastRestocked = DateTime.UtcNow,
                    Notes = request.Notes,
                    IsActive = true
                };

                _context.Inventory.Add(inventoryItem);

                // Ürünün stok miktarını güncelle
                product.StockQuantity = request.InitialStock;
                product.MinStockLevel = request.MinStockLevel;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Oluşturulan inventory item'ı döndür
                var createdItem = await _context.Inventory
                    .FirstOrDefaultAsync(i => i.Id == inventoryItem.Id);

                return CreatedAtAction(nameof(GetInventoryItem), new { id = createdItem.Id }, createdItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory item");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/inventory/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateInventoryItem(Guid id, [FromBody] UpdateInventoryItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null || !inventoryItem.IsActive)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                inventoryItem.MinStockLevel = request.MinStockLevel;
                inventoryItem.MaxStockLevel = request.MaxStockLevel;
                inventoryItem.ReorderPoint = request.ReorderPoint;
                inventoryItem.UnitCost = request.UnitCost;
                inventoryItem.Notes = request.Notes;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                // Ürünün min stock level'ını güncelle
                var product = await _context.Products.FindAsync(inventoryItem.ProductId);
                if (product != null)
                {
                    product.MinStockLevel = request.MinStockLevel;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/inventory/{id}/restock
        [HttpPost("{id}/restock")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> RestockInventory(Guid id, [FromBody] RestockRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null || !inventoryItem.IsActive)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                if (request.Quantity <= 0)
                {
                    return BadRequest(new { message = "Restock quantity must be greater than zero" });
                }

                // Stok miktarını güncelle
                inventoryItem.CurrentStock += request.Quantity;
                inventoryItem.LastRestocked = DateTime.UtcNow;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                // Ürünün stok miktarını güncelle
                var product = await _context.Products.FindAsync(inventoryItem.ProductId);
                if (product != null)
                {
                    product.StockQuantity = inventoryItem.CurrentStock;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                // Restock log'u oluştur
                var restockLog = new InventoryTransaction
                {
                    InventoryId = id,
                    TransactionType = TransactionType.Restock,
                    Quantity = request.Quantity,
                    UnitCost = request.UnitCost ?? inventoryItem.UnitCost,
                    TotalCost = (request.UnitCost ?? inventoryItem.UnitCost) * request.Quantity,
                    Notes = request.Notes,
                    TransactionDate = DateTime.UtcNow
                };

                _context.InventoryTransactions.Add(restockLog);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory restocked successfully", newStock = inventoryItem.CurrentStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restocking inventory {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/inventory/{id}/adjust
        [HttpPost("{id}/adjust")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> AdjustInventory(Guid id, [FromBody] AdjustInventoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null || !inventoryItem.IsActive)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                var newStock = inventoryItem.CurrentStock + request.Adjustment;
                if (newStock < 0)
                {
                    return BadRequest(new { message = "Adjustment would result in negative stock" });
                }

                // Stok miktarını güncelle
                inventoryItem.CurrentStock = newStock;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                // Ürünün stok miktarını güncelle
                var product = await _context.Products.FindAsync(inventoryItem.ProductId);
                if (product != null)
                {
                    product.StockQuantity = newStock;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                // Adjustment log'u oluştur
                var adjustmentLog = new InventoryTransaction
                {
                    InventoryId = id,
                    TransactionType = request.Adjustment > 0 ? TransactionType.Adjustment : TransactionType.Loss,
                    Quantity = Math.Abs(request.Adjustment),
                    UnitCost = inventoryItem.UnitCost,
                    TotalCost = inventoryItem.UnitCost * Math.Abs(request.Adjustment),
                    Notes = request.Reason,
                    TransactionDate = DateTime.UtcNow
                };

                _context.InventoryTransactions.Add(adjustmentLog);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory adjusted successfully", newStock = newStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting inventory {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/inventory/low-stock
        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetLowStockItems()
        {
            try
            {
                var lowStockItems = await _context.Inventory
                    .Where(i => i.IsActive && i.CurrentStock <= i.MinStockLevel)
                    .OrderBy(i => i.CurrentStock)
                    .ToListAsync();

                // Product bilgilerini ayrı sorgu ile al
                var productIds = lowStockItems.Select(i => i.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

                // Low stock item'ları product bilgileri ile zenginleştir
                var enrichedLowStockItems = lowStockItems.Select(item => {
                    var product = products.TryGetValue(item.ProductId, out var p) ? p : null;
                    return new
                    {
                        item.Id,
                        item.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        ProductCategory = product?.Category ?? "Unknown Category",
                        item.CurrentStock,
                        item.MinStockLevel,
                        item.MaxStockLevel,
                        item.ReorderPoint,
                        item.UnitCost,
                        item.LastRestocked,
                        item.Notes,
                        item.IsActive
                    };
                }).ToList();

                return Ok(enrichedLowStockItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting low stock items");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/inventory/transactions/{id}
        [HttpGet("transactions/{id}")]
        public async Task<ActionResult<IEnumerable<InventoryTransaction>>> GetInventoryTransactions(Guid id)
        {
            try
            {
                var transactions = await _context.InventoryTransactions
                    .Where(t => t.InventoryId == id)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory transactions {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/inventory/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteInventoryItem(Guid id)
        {
            try
            {
                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                // Soft delete
                inventoryItem.IsActive = false;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Inventory item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class CreateInventoryItemRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public int InitialStock { get; set; }

        [Required]
        public int MinStockLevel { get; set; }

        public int? MaxStockLevel { get; set; }

        public int? ReorderPoint { get; set; }

        [Required]
        public decimal UnitCost { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateInventoryItemRequest
    {
        [Required]
        public int MinStockLevel { get; set; }

        public int? MaxStockLevel { get; set; }

        public int? ReorderPoint { get; set; }

        [Required]
        public decimal UnitCost { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class RestockRequest
    {
        [Required]
        public int Quantity { get; set; }

        public decimal? UnitCost { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class AdjustInventoryRequest
    {
        [Required]
        public int Adjustment { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
