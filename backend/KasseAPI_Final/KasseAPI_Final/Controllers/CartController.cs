using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartController> _logger;

        public CartController(AppDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/cart/{cartId}
        [HttpGet("{cartId}")]
        public async Task<IActionResult> GetCart(string cartId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart
        [HttpPost]
        public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                var cartId = Guid.NewGuid().ToString();
                var cart = new Cart
                {
                    CartId = cartId,
                    TableNumber = request.TableNumber,
                    WaiterName = request.WaiterName,
                    CustomerId = !string.IsNullOrEmpty(request.CustomerId) && Guid.TryParse(request.CustomerId, out var customerGuid) ? customerGuid : (Guid?)null,
                    UserId = userId,
                    Notes = request.Notes,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Status = CartStatus.Active
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cart created successfully", cartId = cart.CartId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/{cartId}/items
        [HttpPost("{cartId}/items")]
        public async Task<IActionResult> AddItemToCart(string cartId, [FromBody] AddCartItemRequest request)
        {
            try
            {
                var cart = await _context.Carts.FindAsync(cartId);
                if (cart == null || cart.Status != CartStatus.Active)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return NotFound(new { message = "Product not found" });
                }

                var cartItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price,
                    Notes = request.Notes
                };

                _context.CartItems.Add(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Item added to cart successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/cart/{cartId}/items/{itemId}
        [HttpDelete("{cartId}/items/{itemId}")]
        public async Task<IActionResult> RemoveItemFromCart(string cartId, Guid itemId)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.Id == itemId);

                if (cartItem == null)
                {
                    return NotFound(new { message = "Cart item not found" });
                }

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Item removed from cart successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/cart/{cartId}/items/{itemId}
        [HttpPut("{cartId}/items/{itemId}")]
        public async Task<IActionResult> UpdateCartItem(string cartId, Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.Id == itemId);

                if (cartItem == null)
                {
                    return NotFound(new { message = "Cart item not found" });
                }

                cartItem.Quantity = request.Quantity;
                cartItem.Notes = request.Notes;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Cart item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/cart/{cartId}
        [HttpDelete("{cartId}")]
        public async Task<IActionResult> ClearCart(string cartId)
        {
            try
            {
                var cart = await _context.Carts.FindAsync(cartId);
                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found" });
                }

                var cartItems = await _context.CartItems
                    .Where(ci => ci.CartId == cartId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);
                _context.Carts.Remove(cart);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cart cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class CreateCartRequest
    {
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public string? CustomerId { get; set; }
        public string? Notes { get; set; }
    }

    public class AddCartItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}
