using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Registrierkasse_API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TableController : ControllerBase
    {
        private readonly ITableService _tableService;
        private readonly AppDbContext _context;
        private readonly ILogger<TableController> _logger;

        public TableController(ITableService tableService, AppDbContext context, ILogger<TableController> logger)
        {
            _tableService = tableService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<Table>>> GetAllTables()
        {
            try
            {
                var tables = await _tableService.GetAllTablesAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve tables", details = ex.Message });
            }
        }

        [HttpGet("{tableNumber}")]
        public async Task<ActionResult<Table>> GetTable(int tableNumber)
        {
            try
            {
                var table = await _tableService.GetTableByNumberAsync(tableNumber);
                if (table == null)
                {
                    return NotFound(new { error = $"Table {tableNumber} not found" });
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve table", details = ex.Message });
            }
        }

        // Masa birleştirme endpoint'i
        [HttpPost("merge")]
        public async Task<ActionResult<MergeTablesResponse>> MergeTables([FromBody] MergeTablesRequest request)
        {
            try
            {
                _logger.LogInformation("Merging tables: {TableIds}", string.Join(", ", request.TableIds));

                // Seçili masaları kontrol et
                var tables = await _context.Tables
                    .Include(t => t.CurrentCart)
                        .ThenInclude(c => c.Items)
                    .Where(t => request.TableIds.Contains(t.Number))
                    .ToListAsync();

                if (tables.Count != request.TableIds.Count)
                {
                    return BadRequest(new { error = "Some tables not found" });
                }

                // Aktif cart'ları topla
                var activeCarts = tables.Where(t => t.CurrentCartId != null).ToList();
                if (activeCarts.Count == 0)
                {
                    return BadRequest(new { error = "No active carts to merge" });
                }

                // Yeni birleştirilmiş cart oluştur
                var mergedCart = new Cart
                {
                    CartId = Guid.NewGuid().ToString(),
                    TableNumber = string.Join(", ", request.TableIds),
                    WaiterName = activeCarts.First().CurrentCart?.WaiterName,
                    Status = CartStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                // Tüm ürünleri yeni cart'a ekle
                var allItems = new List<CartItem>();
                foreach (var table in activeCarts)
                {
                    if (table.CurrentCart?.Items != null)
                    {
                        foreach (var item in table.CurrentCart.Items)
                        {
                            var existingItem = allItems.FirstOrDefault(i => i.ProductId == item.ProductId);
                            if (existingItem != null)
                            {
                                existingItem.Quantity += item.Quantity;
                                existingItem.TotalAmount = existingItem.Quantity * existingItem.UnitPrice;
                            }
                            else
                            {
                                allItems.Add(new CartItem
                                {
                                    CartId = mergedCart.CartId,
                                    ProductId = item.ProductId,
                                    ProductName = item.ProductName,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.UnitPrice,
                                    TaxRate = item.TaxRate,
                                    TotalAmount = item.TotalAmount,
                                    Notes = item.Notes
                                });
                            }
                        }
                    }
                }

                mergedCart.Items = allItems;

                // Toplamları hesapla
                await UpdateCartTotals(mergedCart);

                // Masaları güncelle
                foreach (var table in tables)
                {
                    table.CurrentCartId = mergedCart.CartId;
                    table.Status = "occupied";
                    table.CurrentTotal = mergedCart.TotalAmount;
                }

                // Eski cart'ları kapat
                foreach (var table in activeCarts)
                {
                    if (table.CurrentCart != null)
                    {
                        table.CurrentCart.Status = CartStatus.Cancelled;
                    }
                }

                _context.Carts.Add(mergedCart);
                await _context.SaveChangesAsync();

                return Ok(new MergeTablesResponse
                {
                    MergedCart = mergedCart,
                    AffectedTables = tables.Select(t => new { t.Number, t.Name }).Cast<dynamic>().ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging tables");
                return StatusCode(500, new { error = "Failed to merge tables", details = ex.Message });
            }
        }

        // Masa ayırma endpoint'i
        [HttpPost("split")]
        public async Task<ActionResult<SplitTableResponse>> SplitTable([FromBody] SplitTableRequest request)
        {
            try
            {
                _logger.LogInformation("Splitting table {TableId} with {ItemCount} items", request.TableId, request.ItemsToSplit.Count);

                var table = await _context.Tables
                    .Include(t => t.CurrentCart)
                        .ThenInclude(c => c.Items)
                    .FirstOrDefaultAsync(t => t.Number == request.TableId);

                if (table == null)
                {
                    return NotFound(new { error = $"Table {request.TableId} not found" });
                }

                if (table.CurrentCart == null)
                {
                    return BadRequest(new { error = "No active cart to split" });
                }

                // Yeni cart oluştur
                var newCart = new Cart
                {
                    CartId = Guid.NewGuid().ToString(),
                    TableNumber = request.TableId.ToString(),
                    WaiterName = table.CurrentCart.WaiterName,
                    Status = CartStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                // Seçili ürünleri yeni cart'a taşı
                var itemsToMove = table.CurrentCart.Items
                    .Where(i => request.ItemsToSplit.Contains(i.Id))
                    .ToList();

                var newCartItems = itemsToMove.Select(item => new CartItem
                {
                    CartId = newCart.CartId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TaxRate = item.TaxRate,
                    TotalAmount = item.TotalAmount,
                    Notes = item.Notes
                }).ToList();

                newCart.Items = newCartItems;

                // Eski cart'tan ürünleri çıkar
                foreach (var item in itemsToMove)
                {
                    table.CurrentCart.Items.Remove(item);
                }

                // Toplamları güncelle
                await UpdateCartTotals(newCart);
                await UpdateCartTotals(table.CurrentCart);

                // Yeni masayı oluştur (geçici)
                var newTable = new Table
                {
                    Number = await GetNextTableNumber(),
                    Name = $"Split from Masa {request.TableId}",
                    Capacity = table.Capacity,
                    Location = table.Location,
                    Status = "occupied",
                    CurrentCartId = newCart.CartId,
                    CurrentTotal = newCart.TotalAmount,
                    StartTime = DateTime.UtcNow
                };

                _context.Carts.Add(newCart);
                _context.Tables.Add(newTable);
                await _context.SaveChangesAsync();

                return Ok(new SplitTableResponse
                {
                    OriginalTable = table,
                    NewTable = newTable,
                    NewCart = newCart
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error splitting table");
                return StatusCode(500, new { error = "Failed to split table", details = ex.Message });
            }
        }

        // Açık masaları getir
        [HttpGet("open")]
        public async Task<ActionResult<List<Table>>> GetOpenTables()
        {
            try
            {
                var openTables = await _context.Tables
                    .Include(t => t.CurrentCart)
                        .ThenInclude(c => c.Items)
                    .Where(t => t.Status == "occupied" && t.CurrentCartId != null)
                    .OrderBy(t => t.Number)
                    .ToListAsync();

                return Ok(openTables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting open tables");
                return StatusCode(500, new { error = "Failed to get open tables", details = ex.Message });
            }
        }

        // Masa durumunu güncelle
        [HttpPut("{tableNumber}/status")]
        public async Task<ActionResult<Table>> UpdateTableStatus(int tableNumber, [FromBody] UpdateTableStatusRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableStatusAsync(tableNumber, request.Status, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table status", details = ex.Message });
            }
        }

        // Masa siparişini güncelle
        [HttpPut("{tableNumber}/order")]
        public async Task<ActionResult<Table>> UpdateTableOrder(int tableNumber, [FromBody] UpdateTableOrderRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableOrderAsync(tableNumber, request.OrderId, request.Total);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table order", details = ex.Message });
            }
        }

        // Masa siparişini tamamla
        [HttpPost("{tableNumber}/complete")]
        public async Task<ActionResult<Table>> CompleteTableOrder(int tableNumber, [FromBody] CompleteTableOrderRequest request)
        {
            try
            {
                var table = await _tableService.CompleteTableOrderAsync(tableNumber, request.PaidAmount);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to complete table order", details = ex.Message });
            }
        }

        // Masayı temizle
        [HttpPost("{tableNumber}/clear")]
        public async Task<ActionResult<Table>> ClearTable(int tableNumber)
        {
            try
            {
                var table = await _tableService.ClearTableAsync(tableNumber);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to clear table", details = ex.Message });
            }
        }

        // Masa rezervasyonu
        [HttpPost("{tableNumber}/reserve")]
        public async Task<ActionResult<Table>> ReserveTable(int tableNumber, [FromBody] ReserveTableRequest request)
        {
            try
            {
                var table = await _tableService.ReserveTableAsync(tableNumber, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to reserve table", details = ex.Message });
            }
        }

        // Masa sipariş geçmişi
        [HttpGet("{tableNumber}/history")]
        public async Task<ActionResult<List<Order>>> GetTableOrderHistory(int tableNumber)
        {
            try
            {
                var orders = await _tableService.GetTableOrderHistoryAsync(tableNumber);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve table order history", details = ex.Message });
            }
        }

        // Masa müşteri bilgisini güncelle
        [HttpPut("{tableNumber}/customer")]
        public async Task<ActionResult<Table>> UpdateTableCustomer(int tableNumber, [FromBody] UpdateTableCustomerRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableCustomerAsync(tableNumber, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table customer", details = ex.Message });
            }
        }

        // Yardımcı metodlar
        private async Task UpdateCartTotals(Cart cart)
        {
            cart.Subtotal = cart.Items.Sum(i => i.TotalAmount);
            cart.TaxAmount = cart.Items.Sum(i => i.TaxAmount);
            cart.TotalAmount = cart.Subtotal + cart.TaxAmount - cart.DiscountAmount;
            cart.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<int> GetNextTableNumber()
        {
            var maxNumber = await _context.Tables.MaxAsync(t => t.Number);
            return maxNumber + 1;
        }
    }

    // Request/Response modelleri
    public class MergeTablesRequest
    {
        public List<int> TableIds { get; set; } = new List<int>();
    }

    public class MergeTablesResponse
    {
        public Cart MergedCart { get; set; } = null!;
        public List<dynamic> AffectedTables { get; set; } = new List<dynamic>();
    }

    public class SplitTableRequest
    {
        public int TableId { get; set; }
        public List<Guid> ItemsToSplit { get; set; } = new List<Guid>();
    }

    public class SplitTableResponse
    {
        public Table OriginalTable { get; set; } = null!;
        public Table NewTable { get; set; } = null!;
        public Cart NewCart { get; set; } = null!;
    }

    public class UpdateTableStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
    }

    public class UpdateTableOrderRequest
    {
        public Guid OrderId { get; set; }
        public decimal Total { get; set; }
    }

    public class CompleteTableOrderRequest
    {
        public decimal PaidAmount { get; set; }
    }

    public class ReserveTableRequest
    {
        public string CustomerName { get; set; } = string.Empty;
    }

    public class UpdateTableCustomerRequest
    {
        public string CustomerName { get; set; } = string.Empty;
    }
} 
