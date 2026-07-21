using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Inventory;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryController> _logger;
        private readonly IAuditLogService _auditLog;
        private readonly ISettingsTenantResolver _settingsTenantResolver;

        public InventoryController(
            AppDbContext context,
            ILogger<InventoryController> logger,
            IAuditLogService auditLog,
            ISettingsTenantResolver settingsTenantResolver)
        {
            _context = context;
            _logger = logger;
            _auditLog = auditLog;
            _settingsTenantResolver = settingsTenantResolver;
        }

        private string ActorUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        private string ActorRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";

        /// <summary>Active inventory row whose product belongs to the tenant (read / mutating ops gate).</summary>
        private Task<bool> ActiveInventoryItemBelongsToTenantAsync(Guid inventoryItemId, Guid tenantId, CancellationToken cancellationToken = default) =>
            (from i in _context.Inventory.AsNoTracking()
             join p in _context.Products.AsNoTracking() on i.ProductId equals p.Id
             where i.Id == inventoryItemId && i.IsActive && p.TenantId == tenantId
             select 1).AnyAsync(cancellationToken);

        /// <summary>Any inventory row (active or not) linked to a product in the tenant — e.g. soft delete.</summary>
        private Task<bool> InventoryRowLinkedToTenantProductAsync(Guid inventoryItemId, Guid tenantId, CancellationToken cancellationToken = default) =>
            (from i in _context.Inventory.AsNoTracking()
             join p in _context.Products.AsNoTracking() on i.ProductId equals p.Id
             where i.Id == inventoryItemId && p.TenantId == tenantId
             select 1).AnyAsync(cancellationToken);

        /// <summary>
        /// Audit trail for stock-changing operations (best-effort: failure does not roll back stock).
        /// </summary>
        private async Task TryAuditInventoryAsync(
            string action,
            Guid entityId,
            object? oldValues = null,
            object? newValues = null,
            string? notes = null)
        {
            try
            {
                await _auditLog.LogEntityChangeAsync(
                    action,
                    "InventoryItem",
                    entityId,
                    ActorUserId,
                    ActorRole,
                    oldValues,
                    newValues,
                    description: $"Inventory: {action}",
                    notes: notes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log failed for inventory action {Action} entity {EntityId}", action, entityId);
            }
        }

        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetInventory()
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var rows = await (
                    from i in _context.Inventory
                    join p in _context.Products on i.ProductId equals p.Id
                    where i.IsActive && p.TenantId == tenantId
                    select new { i, p }).ToListAsync();

                var enrichedInventory = rows
                    .Select(x => new
                    {
                        x.i.Id,
                        x.i.ProductId,
                        ProductName = x.p.Name,
                        ProductCategory = x.p.Category,
                        x.i.CurrentStock,
                        x.i.MinStockLevel,
                        x.i.MaxStockLevel,
                        x.i.ReorderPoint,
                        x.i.UnitCost,
                        x.i.LastRestocked,
                        x.i.Notes,
                        x.i.IsActive
                    })
                    .OrderBy(r => r.ProductName)
                    .ToList();

                return Ok(enrichedInventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>Global movement history (paginated). Optional filters.</summary>
        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet("history")]
        public async Task<ActionResult<InventoryHistoryPageDto>> GetInventoryHistory(
            [FromQuery] Guid? inventoryId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var take = Math.Clamp(pageSize, 1, 200);
                var skip = Math.Max(0, (Math.Max(1, page) - 1) * take);
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

                if (inventoryId.HasValue)
                {
                    var ownerTenant = await (
                        from i in _context.Inventory.AsNoTracking()
                        join p in _context.Products.AsNoTracking() on i.ProductId equals p.Id
                        where i.Id == inventoryId.Value
                        select p.TenantId).FirstOrDefaultAsync(cancellationToken);
                    if (ownerTenant != default && ownerTenant != tenantId)
                        return NotFound(new { message = "Inventory item not found" });
                }

                var q =
                    from t in _context.InventoryTransactions.AsNoTracking()
                    join inv in _context.Inventory.AsNoTracking() on t.InventoryId equals inv.Id
                    join p in _context.Products.AsNoTracking() on inv.ProductId equals p.Id
                    where inv.IsActive && p.TenantId == tenantId
                    select new { t, inv, p };

                if (inventoryId.HasValue)
                    q = q.Where(x => x.inv.Id == inventoryId.Value);

                if (fromUtc.HasValue)
                    q = q.Where(x => x.t.TransactionDate >= fromUtc.Value);

                if (toUtc.HasValue)
                    q = q.Where(x => x.t.TransactionDate <= toUtc.Value);

                var total = await q.CountAsync(cancellationToken);

                var rows = await q
                    .OrderByDescending(x => x.t.TransactionDate)
                    .Skip(skip)
                    .Take(take)
                    .Select(x => new InventoryHistoryRowDto
                    {
                        TransactionId = x.t.Id,
                        InventoryId = x.inv.Id,
                        ProductId = x.p.Id,
                        ProductName = x.p.Name ?? "",
                        ProductCategory = x.p.Category ?? "",
                        TransactionType = x.t.TransactionType,
                        Quantity = x.t.Quantity,
                        UnitCost = x.t.UnitCost,
                        TotalCost = x.t.TotalCost,
                        Notes = x.t.Notes,
                        TransactionDateUtc = x.t.TransactionDate
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new InventoryHistoryPageDto
                {
                    Items = rows,
                    TotalCount = total,
                    Page = page,
                    PageSize = take
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing inventory history");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>Reorder foundation: items at or below reorder point / min stock.</summary>
        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet("reorder-suggestions")]
        public async Task<ActionResult<IEnumerable<ReorderSuggestionDto>>> GetReorderSuggestions(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
                var rows = await (
                    from i in _context.Inventory.AsNoTracking()
                    join p in _context.Products.AsNoTracking() on i.ProductId equals p.Id
                    where i.IsActive && p.TenantId == tenantId
                    select new { i, p }).ToListAsync(cancellationToken);

                var result = new List<ReorderSuggestionDto>();
                foreach (var x in rows)
                {
                    var threshold = x.i.ReorderPoint ?? x.i.MinStockLevel;
                    if (x.i.CurrentStock > threshold)
                        continue;

                    var gap = threshold - x.i.CurrentStock;
                    var suggested = gap >= 0 ? Math.Max(1, gap) : 1;

                    result.Add(new ReorderSuggestionDto
                    {
                        InventoryId = x.i.Id,
                        ProductId = x.i.ProductId,
                        ProductName = x.p.Name ?? "",
                        ProductCategory = x.p.Category ?? "",
                        CurrentStock = x.i.CurrentStock,
                        MinStockLevel = x.i.MinStockLevel,
                        ReorderPoint = x.i.ReorderPoint,
                        SuggestedOrderQuantity = suggested
                    });
                }

                return Ok(result.OrderBy(r => r.CurrentStock).ThenBy(r => r.ProductName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building reorder suggestions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet("low-stock")]
        public async Task<ActionResult<IEnumerable<object>>> GetLowStockItems()
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var rows = await (
                    from i in _context.Inventory
                    join p in _context.Products on i.ProductId equals p.Id
                    where i.IsActive && i.CurrentStock <= i.MinStockLevel && p.TenantId == tenantId
                    orderby i.CurrentStock
                    select new { i, p }).ToListAsync();

                var enrichedLowStockItems = rows
                    .Select(x => new
                    {
                        x.i.Id,
                        x.i.ProductId,
                        ProductName = x.p.Name,
                        ProductCategory = x.p.Category,
                        x.i.CurrentStock,
                        x.i.MinStockLevel,
                        x.i.MaxStockLevel,
                        x.i.ReorderPoint,
                        x.i.UnitCost,
                        x.i.LastRestocked,
                        x.i.Notes,
                        x.i.IsActive
                    })
                    .ToList();

                return Ok(enrichedLowStockItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting low stock items");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet("transactions/{id}")]
        public async Task<ActionResult<IEnumerable<InventoryTransaction>>> GetInventoryTransactions(Guid id)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await ActiveInventoryItemBelongsToTenantAsync(id, tenantId))
                    return NotFound(new { message = "Inventory item not found" });

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

        [HasPermission(AppPermissions.InventoryView)]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> GetInventoryItem(Guid id)
        {
            try
            {
                var tenantIdItem = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var row = await (
                    from i in _context.Inventory
                    join p in _context.Products on i.ProductId equals p.Id
                    where i.Id == id && i.IsActive && p.TenantId == tenantIdItem
                    select new { i, p }).FirstOrDefaultAsync();

                if (row == null)
                    return NotFound(new { message = "Inventory item not found" });

                var enrichedItem = new
                {
                    row.i.Id,
                    row.i.ProductId,
                    ProductName = row.p.Name,
                    ProductCategory = row.p.Category,
                    row.i.CurrentStock,
                    row.i.MinStockLevel,
                    row.i.MaxStockLevel,
                    row.i.ReorderPoint,
                    row.i.UnitCost,
                    row.i.LastRestocked,
                    row.i.Notes,
                    row.i.IsActive
                };

                return Ok(enrichedItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost]
        [HasPermission(AppPermissions.InventoryManage)]
        public async Task<ActionResult<InventoryItem>> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tenantIdCreate = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == tenantIdCreate);
                if (product == null)
                {
                    return NotFound(new { message = "Product not found" });
                }

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

                product.StockQuantity = request.InitialStock;
                product.MinStockLevel = request.MinStockLevel;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await TryAuditInventoryAsync(
                    "INVENTORY_CREATE",
                    inventoryItem.Id,
                    null,
                    new
                    {
                        inventoryItem.ProductId,
                        inventoryItem.CurrentStock,
                        inventoryItem.MinStockLevel
                    });

                var createdItem = await _context.Inventory
                    .FirstOrDefaultAsync(i => i.Id == inventoryItem.Id);

                return CreatedAtAction(nameof(GetInventoryItem), new { id = createdItem!.Id }, createdItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory item");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{id:guid}")]
        [HasPermission(AppPermissions.InventoryManage)]
        public async Task<IActionResult> UpdateInventoryItem(Guid id, [FromBody] UpdateInventoryItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tenantIdUpd = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await ActiveInventoryItemBelongsToTenantAsync(id, tenantIdUpd))
                    return NotFound(new { message = "Inventory item not found" });

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null || !inventoryItem.IsActive)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                var before = new
                {
                    inventoryItem.MinStockLevel,
                    inventoryItem.MaxStockLevel,
                    inventoryItem.ReorderPoint,
                    inventoryItem.UnitCost,
                    inventoryItem.Notes
                };

                inventoryItem.MinStockLevel = request.MinStockLevel;
                inventoryItem.MaxStockLevel = request.MaxStockLevel;
                inventoryItem.ReorderPoint = request.ReorderPoint;
                inventoryItem.UnitCost = request.UnitCost;
                inventoryItem.Notes = request.Notes;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == inventoryItem.ProductId && p.TenantId == tenantIdUpd);
                if (product != null)
                {
                    product.MinStockLevel = request.MinStockLevel;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                await TryAuditInventoryAsync(
                    "INVENTORY_UPDATE",
                    id,
                    before,
                    new
                    {
                        request.MinStockLevel,
                        request.MaxStockLevel,
                        request.ReorderPoint,
                        request.UnitCost,
                        request.Notes
                    });

                return Ok(new { message = "Inventory item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("{id:guid}/restock")]
        [HasPermission(AppPermissions.InventoryManage)]
        public async Task<IActionResult> RestockInventory(Guid id, [FromBody] RestockRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tenantIdRestock = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await ActiveInventoryItemBelongsToTenantAsync(id, tenantIdRestock))
                    return NotFound(new { message = "Inventory item not found" });

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null || !inventoryItem.IsActive)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                if (request.Quantity <= 0)
                {
                    return BadRequest(new { message = "Restock quantity must be greater than zero" });
                }

                var previousStock = inventoryItem.CurrentStock;

                inventoryItem.CurrentStock += request.Quantity;
                inventoryItem.LastRestocked = DateTime.UtcNow;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == inventoryItem.ProductId && p.TenantId == tenantIdRestock);
                if (product != null)
                {
                    product.StockQuantity = inventoryItem.CurrentStock;
                    product.UpdatedAt = DateTime.UtcNow;
                }

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

                await TryAuditInventoryAsync(
                    "INVENTORY_RESTOCK",
                    id,
                    new { previousStock },
                    new { newStock = inventoryItem.CurrentStock, request.Quantity, request.Notes });

                return Ok(new { message = "Inventory restocked successfully", newStock = inventoryItem.CurrentStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restocking inventory {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>Manuelle Korrektur / Inventur-Varianz (negative Anpassung als Loss).</summary>
        [HttpPost("{id:guid}/adjust")]
        [HasPermission(AppPermissions.InventoryAdjust)]
        public async Task<IActionResult> AdjustInventory(Guid id, [FromBody] AdjustInventoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tenantIdAdj = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await ActiveInventoryItemBelongsToTenantAsync(id, tenantIdAdj))
                    return NotFound(new { message = "Inventory item not found" });

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

                var previousStock = inventoryItem.CurrentStock;

                inventoryItem.CurrentStock = newStock;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == inventoryItem.ProductId && p.TenantId == tenantIdAdj);
                if (product != null)
                {
                    product.StockQuantity = newStock;
                    product.UpdatedAt = DateTime.UtcNow;
                }

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

                await TryAuditInventoryAsync(
                    "INVENTORY_ADJUST",
                    id,
                    new { previousStock },
                    new { newStock, request.Adjustment, request.Reason });

                return Ok(new { message = "Inventory adjusted successfully", newStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting inventory {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>Transfer zwischen zwei Lagerzeilen (gleiche oder verschiedene Produkte).</summary>
        [HttpPost("{id:guid}/transfer")]
        [HasPermission(AppPermissions.InventoryManage)]
        public async Task<IActionResult> TransferInventory(Guid id, [FromBody] TransferInventoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (request.Quantity <= 0)
                {
                    return BadRequest(new { message = "Quantity must be greater than zero" });
                }

                if (request.TargetInventoryId == id)
                {
                    return BadRequest(new { message = "Source and target must differ" });
                }

                var tenantIdXfer = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await ActiveInventoryItemBelongsToTenantAsync(id, tenantIdXfer)
                    || !await ActiveInventoryItemBelongsToTenantAsync(request.TargetInventoryId, tenantIdXfer))
                    return NotFound(new { message = "Source or target inventory not found" });

                var source = await _context.Inventory.FindAsync(id);
                var target = await _context.Inventory.FindAsync(request.TargetInventoryId);

                if (source == null || !source.IsActive || target == null || !target.IsActive)
                {
                    return NotFound(new { message = "Source or target inventory not found" });
                }

                if (source.CurrentStock < request.Quantity)
                {
                    return BadRequest(new { message = "Insufficient stock on source" });
                }

                var sourceStockBefore = source.CurrentStock;
                var targetStockBefore = target.CurrentStock;

                var transferRef = Guid.NewGuid().ToString("N")[..12];
                var note = $"transferRef:{transferRef} | {request.Notes}";

                source.CurrentStock -= request.Quantity;
                source.UpdatedAt = DateTime.UtcNow;

                target.CurrentStock += request.Quantity;
                target.UpdatedAt = DateTime.UtcNow;

                var sourceProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == source.ProductId && p.TenantId == tenantIdXfer);
                if (sourceProduct != null)
                {
                    sourceProduct.StockQuantity = source.CurrentStock;
                    sourceProduct.UpdatedAt = DateTime.UtcNow;
                }

                var targetProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == target.ProductId && p.TenantId == tenantIdXfer);
                if (targetProduct != null)
                {
                    targetProduct.StockQuantity = target.CurrentStock;
                    targetProduct.UpdatedAt = DateTime.UtcNow;
                }

                var unitCost = source.UnitCost;
                var total = unitCost * request.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryId = source.Id,
                    TransactionType = TransactionType.Transfer,
                    Quantity = request.Quantity,
                    UnitCost = unitCost,
                    TotalCost = total,
                    Notes = $"OUT:{target.Id} | {note}",
                    TransactionDate = DateTime.UtcNow
                });

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryId = target.Id,
                    TransactionType = TransactionType.Transfer,
                    Quantity = request.Quantity,
                    UnitCost = unitCost,
                    TotalCost = total,
                    Notes = $"IN:{source.Id} | {note}",
                    TransactionDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                await TryAuditInventoryAsync(
                    "INVENTORY_TRANSFER_OUT",
                    source.Id,
                    new { previousStock = sourceStockBefore },
                    new { newStock = source.CurrentStock, targetId = target.Id, request.Quantity },
                    notes: note);

                await TryAuditInventoryAsync(
                    "INVENTORY_TRANSFER_IN",
                    target.Id,
                    new { previousStock = targetStockBefore },
                    new { newStock = target.CurrentStock, sourceId = source.Id, request.Quantity },
                    notes: note);

                return Ok(new
                {
                    message = "Transfer completed",
                    transferRef,
                    sourceNewStock = source.CurrentStock,
                    targetNewStock = target.CurrentStock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring inventory {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(AppPermissions.InventoryDelete)]
        public async Task<IActionResult> DeleteInventoryItem(Guid id)
        {
            try
            {
                var tenantIdDel = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                if (!await InventoryRowLinkedToTenantProductAsync(id, tenantIdDel))
                    return NotFound(new { message = "Inventory item not found" });

                var inventoryItem = await _context.Inventory.FindAsync(id);
                if (inventoryItem == null)
                {
                    return NotFound(new { message = "Inventory item not found" });
                }

                var before = new { inventoryItem.ProductId, active = inventoryItem.IsActive };

                // Soft delete
                inventoryItem.IsActive = false;
                inventoryItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await TryAuditInventoryAsync(
                    "INVENTORY_DELETE",
                    id,
                    before,
                    new { active = false });

                return Ok(new { message = "Inventory item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item {InventoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

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
