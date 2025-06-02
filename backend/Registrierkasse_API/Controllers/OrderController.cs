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

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderController> _logger;

        public OrderController(AppDbContext context, ILogger<OrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            try
            {
                return await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Where(o => o.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Siparişler alınırken hata oluştu");
                return StatusCode(500, "Siparişler alınırken bir hata oluştu");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

                if (order == null)
                {
                    return NotFound($"Sipariş bulunamadı: {id}");
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş alınırken hata oluştu: {OrderId}", id);
                return StatusCode(500, "Sipariş alınırken bir hata oluştu");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Cashier")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderModel model)
        {
            try
            {
                var order = new Order
                {
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 20),
                    CustomerId = model.CustomerId,
                    TableNumber = model.TableNumber,
                    WaiterName = model.WaiterName,
                    Status = "pending",
                    Notes = model.Notes,
                    CreatedBy = User.Identity?.Name
                };

                foreach (var item in model.Items)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.IsActive);

                    if (product == null)
                    {
                        return BadRequest($"Ürün bulunamadı: {item.ProductId}");
                    }

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Notes = item.Notes,
                        CreatedBy = User.Identity?.Name
                    });
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş oluşturulurken hata oluştu");
                return StatusCode(500, "Sipariş oluşturulurken bir hata oluştu");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Cashier")]
        public async Task<IActionResult> UpdateOrder(Guid id, [FromBody] Order order)
        {
            if (id != order.Id)
            {
                return BadRequest();
            }

            _context.Entry(order).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool OrderExists(Guid id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, UpdateOrderStatusModel model)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

                if (order == null)
                {
                    return NotFound($"Sipariş bulunamadı: {id}");
                }

                order.Status = model.Status;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = User.Identity?.Name;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Sipariş durumu güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş durumu güncellenirken hata oluştu: {OrderId}", id);
                return StatusCode(500, "Sipariş durumu güncellenirken bir hata oluştu");
            }
        }

        [HttpPut("{orderId}/items/{itemId}/status")]
        public async Task<IActionResult> UpdateOrderItemStatus(Guid orderId, Guid itemId, [FromBody] UpdateOrderItemStatusModel model)
        {
            try
            {
                var orderItem = await _context.OrderItems
                    .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.Id == itemId);

                if (orderItem == null)
                {
                    return NotFound(new { message = "Sipariş kalemi bulunamadı" });
                }

                orderItem.Status = model.Status;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Sipariş kalemi durumu güncellendi: Sipariş #{{orderId}}, Kalem #{{itemId}} - {{model.Status}}");

                return Ok(new { message = "Sipariş kalemi durumu başarıyla güncellendi", orderItem });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Sipariş #{{orderId}}, Kalem #{{itemId}} durumu güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Sipariş kalemi durumu güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveOrders()
        {
            try
            {
                var activeOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(o => o.Status != "completed" && o.Status != "cancelled")
                    .OrderByDescending(o => o.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Aktif siparişler başarıyla getirildi", activeOrders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aktif siparişler getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Aktif siparişler getirilirken bir hata oluştu", error = ex.Message });
            }
        }
    }

    public class CreateOrderModel
    {
        public Guid? CustomerId { get; set; }
        public int TableNumber { get; set; }
        public string WaiterName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<OrderItemModel> Items { get; set; } = new List<OrderItemModel>();
    }

    public class OrderItemModel
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class UpdateOrderStatusModel
    {
        public string Status { get; set; } = string.Empty; // pending, in_progress, completed, cancelled
    }

    public class UpdateOrderItemStatusModel
    {
        public string Status { get; set; } = string.Empty; // pending, in_progress, ready, served, cancelled
    }
} 