using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(AppDbContext context, ILogger<OrdersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.Items)
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Items)
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound(new { message = "Order not found" });
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/orders
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Benzersiz order_id oluştur
                var orderId = GenerateOrderId();
                
                // Sipariş oluştur
                var order = new Order
                {
                    OrderId = orderId,
                    TableNumber = request.TableNumber,
                    WaiterName = request.WaiterName,
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    Notes = request.Notes,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending
                };

                // Order items oluştur ve hesaplamaları yap
                var orderItems = new List<OrderItem>();
                decimal subtotal = 0;
                decimal totalTax = 0;
                decimal totalDiscount = 0;

                foreach (var item in request.Items)
                {
                    // Ürün bilgilerini al
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        return BadRequest($"Product with ID {item.ProductId} not found");
                    }

                    // Vergi oranını hesapla
                    decimal taxRate = GetTaxRate(product.TaxType);
                    decimal unitPrice = product.Price;
                    decimal itemSubtotal = unitPrice * item.Quantity;
                    decimal itemTax = itemSubtotal * taxRate;
                    decimal itemDiscount = 0;
                    decimal itemTotal = itemSubtotal + itemTax - itemDiscount;

                    var orderItem = new OrderItem
                    {
                        OrderId = orderId,
                        ProductId = item.ProductId,
                        ProductName = product.Name,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        TaxRate = taxRate,
                        TaxAmount = itemTax,
                        DiscountAmount = itemDiscount,
                        TotalAmount = itemTotal,
                        SpecialNotes = item.SpecialNotes,
                        ProductDescription = product.Description,
                        ProductCategory = product.Category
                    };

                    orderItems.Add(orderItem);

                    // Toplamları güncelle
                    subtotal += itemSubtotal;
                    totalTax += itemTax;
                    totalDiscount += itemDiscount;
                }

                // Order'a hesaplamaları ata
                order.Subtotal = subtotal;
                order.TaxAmount = totalTax;
                order.DiscountAmount = totalDiscount;
                order.TotalAmount = subtotal + totalTax - totalDiscount;

                // Order ve OrderItems'ları kaydet
                _context.Orders.Add(order);
                _context.OrderItems.AddRange(orderItems);
                await _context.SaveChangesAsync();

                // Oluşturulan order'ı döndür
                var createdOrder = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/orders/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    return NotFound(new { message = "Order not found" });
                }

                order.Status = request.Status;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Order status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/orders/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    return NotFound(new { message = "Order not found" });
                }

                // Soft delete
                order.IsActive = false;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Order deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {OrderId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/orders/status/{status}
        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByStatus(OrderStatus status)
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.Items)
                    .Where(o => o.Status == status && o.IsActive)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by status {Status}", status);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Helper methods
        private string GenerateOrderId()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        private decimal GetTaxRate(int taxType)
        {
            return TaxTypes.GetTaxRate(taxType) / 100.0m;
        }
    }

    // DTOs
    public class CreateOrderRequest
    {
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Notes { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? SpecialNotes { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
    }
}
