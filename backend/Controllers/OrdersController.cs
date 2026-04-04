using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers
{
    [HasPermission(AppPermissions.OrderCreate)]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly ISettingsTenantResolver _settingsTenantResolver;

        public OrdersController(
            AppDbContext context,
            ILogger<OrdersController> logger,
            ISettingsTenantResolver settingsTenantResolver)
        {
            _context = context;
            _logger = logger;
            _settingsTenantResolver = settingsTenantResolver;
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

                // Sprint 6: Idempotency — if key provided and we already have an order with this key, return it.
                var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
                if (key != null)
                {
                    var existingOrder = await _context.Orders
                        .Include(o => o.Items)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.IdempotencyKey == key);
                    if (existingOrder != null)
                    {
                        _logger.LogInformation("Idempotent order create: returning existing order {OrderId} for key {Key}", existingOrder.Id, key);
                        return Ok(existingOrder);
                    }
                }

                // Benzersiz order_id oluştur
                var orderId = GenerateOrderId();
                
                var order = new Order
                {
                    OrderId = orderId,
                    TableNumber = request.TableNumber,
                    WaiterName = request.WaiterName,
                    CustomerPhone = request.CustomerPhone,
                    Notes = request.Notes,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    IdempotencyKey = key
                };

                if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
                {
                    var linkedCustomer = await _context.Customers.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && c.IsActive);
                    if (linkedCustomer == null)
                    {
                        return BadRequest(new { message = $"Customer with ID {request.CustomerId} not found" });
                    }

                    var orderCustomerKind = CustomerKindResolver.ResolveFromCustomerId(linkedCustomer.Id, request.CustomerKind);
                    _logger.LogDebug(
                        "CreateOrder linked customer CustomerKind={CustomerKind} CustomerId={CustomerId}",
                        orderCustomerKind,
                        linkedCustomer.Id);

                    order.CustomerId = linkedCustomer.Id;
                    order.CustomerName = linkedCustomer.Name;
                }
                else
                {
                    var anonymousKind = request.CustomerKind ?? CustomerKind.WalkIn;
                    _logger.LogDebug("CreateOrder guest/name-only path CustomerKind={CustomerKind}", anonymousKind);
                    // Display snapshot only when no CustomerId — not an authenticated actor identifier.
                    order.CustomerName = request.CustomerName;
                }

                // Order items oluştur ve hesaplamaları yap
                var orderItems = new List<OrderItem>();
                decimal subtotal = 0;
                decimal totalTax = 0;
                decimal totalDiscount = 0;

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                foreach (var item in request.Items)
                {
                    // Ürün bilgilerini al
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == tenantId);
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
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsOrderIdempotencyKeyViolation(ex))
                {
                    if (key != null)
                    {
                        var existing = await _context.Orders
                            .Include(o => o.Items)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(o => o.IdempotencyKey == key);
                        if (existing != null)
                        {
                            _logger.LogInformation("Idempotent order create (race): returning existing order {OrderId} for key {Key}", existing.Id, key);
                            return Ok(existing);
                        }
                    }
                    throw;
                }

                // Oluşturulan order'ı döndür
                var createdOrder = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (createdOrder == null)
                    return StatusCode(500, new { message = "Order was saved but could not be retrieved" });

                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private static bool IsOrderIdempotencyKeyViolation(DbUpdateException ex)
        {
            for (var e = ex.InnerException; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        // PUT: api/orders/{id}/status
        [HttpPut("{id}/status")]
        [HasPermission(AppPermissions.OrderUpdate)]
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
        [HasPermission(AppPermissions.OrderCancel)]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            try
            {
                // TODO: scope check – branch/ownership restriction (e.g. waiter own order, manager any in branch).
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
    /// <summary>
    /// Kitchen/order create payload. <see cref="WaiterName"/> is a display snapshot only (no user FK on <see cref="Order"/>).
    /// Identity for auditing is the authenticated principal on the API, not these name fields.
    /// </summary>
    public class CreateOrderRequest
    {
        public int? TableNumber { get; set; }
        /// <summary>Display-only waiter name on the order ticket; not used for authorization.</summary>
        public string? WaiterName { get; set; }
        public Guid? CustomerId { get; set; }

        /// <summary>Optional explicit customer classification for the order.</summary>
        public CustomerKind? CustomerKind { get; set; }

        /// <summary>Free-text or walk-in label when <see cref="CustomerId"/> is absent; not a substitute for authenticated user id.</summary>
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Notes { get; set; }
        /// <summary>Sprint 6: Optional idempotency key; retries with same key return existing order.</summary>
        [MaxLength(64)]
        public string? IdempotencyKey { get; set; }
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
