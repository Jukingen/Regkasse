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

        // GET: api/cart/current - Belirli masadaki aktif sepeti getir
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUserCart([FromQuery] int? tableNumber = 1)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .Include(c => c.Customer)
                    .Where(c => c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                    .FirstOrDefaultAsync();

                if (cart == null)
                {
                    return NotFound(new { message = $"No active cart found for table {tableNumber}" });
                }

                // Debug: Cart ve CartItem'ları kontrol et
                _logger.LogInformation("GetCurrentUserCart: Table={TableNumber}, CartId={CartId}, ItemsCount={ItemsCount}, UserId={UserId}, CreatedAt={CreatedAt}", 
                    tableNumber, cart.CartId, cart.Items.Count, userId, cart.CreatedAt);
                
                foreach (var item in cart.Items)
                {
                    _logger.LogInformation("CartItem in GetCurrentUserCart: Id={Id}, ProductId={ProductId}, Quantity={Quantity}", 
                        item.Id, item.ProductId, item.Quantity);
                }

                var cartResponse = new CartResponse
                {
                    CartId = cart.CartId,
                    TableNumber = cart.TableNumber,
                    WaiterName = cart.WaiterName,
                    CustomerId = cart.CustomerId,
                    Notes = cart.Notes,
                    Status = cart.Status,
                    CreatedAt = cart.CreatedAt,
                    ExpiresAt = cart.ExpiresAt,
                    Items = cart.Items.Select(ci => new CartItemResponse
                    {
                        Id = ci.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.Product.Name,
                        ProductImage = ci.Product.ImageUrl,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = ci.Product.TaxType.ToString(),
                        TaxRate = GetTaxRate(ci.Product.TaxType)
                    }).ToList(),
                    TotalItems = cart.Items.Sum(ci => ci.Quantity),
                    Subtotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * GetTaxRate(ci.Product.TaxType)),
                    GrandTotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * (1 + GetTaxRate(ci.Product.TaxType)))
                };

                return Ok(cartResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/cart/{cartId} - Belirli bir sepeti getir
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

                var cartResponse = new CartResponse
                {
                    CartId = cart.CartId,
                    TableNumber = cart.TableNumber,
                    WaiterName = cart.WaiterName,
                    CustomerId = cart.CustomerId,
                    Notes = cart.Notes,
                    Status = cart.Status,
                    CreatedAt = cart.CreatedAt,
                    ExpiresAt = cart.ExpiresAt,
                    Items = cart.Items.Select(ci => new CartItemResponse
                    {
                        Id = ci.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.Product.Name,
                        ProductImage = ci.Product.ImageUrl,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = ci.Product.TaxType.ToString(),
                        TaxRate = GetTaxRate(ci.Product.TaxType)
                    }).ToList(),
                    TotalItems = cart.Items.Sum(ci => ci.Quantity),
                    Subtotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * GetTaxRate(ci.Product.TaxType)),
                    GrandTotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * (1 + GetTaxRate(ci.Product.TaxType)))
                };

                return Ok(cartResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart - Yeni sepet oluştur
        [HttpPost]
        public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // Kullanıcının aktif sepeti var mı kontrol et
                var existingCart = await _context.Carts
                    .Where(c => c.UserId == userId && c.Status == CartStatus.Active)
                    .FirstOrDefaultAsync();

                if (existingCart != null)
                {
                    return BadRequest(new { message = "User already has an active cart", cartId = existingCart.CartId });
                }

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

                _logger.LogInformation("Cart created successfully: {CartId} by user {UserId}", cartId, userId);

                return Ok(new { 
                    message = "Cart created successfully", 
                    cartId = cart.CartId,
                    expiresAt = cart.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/add-item - Sepete ürün ekle (otomatik sepet oluşturma ile)
        [HttpPost("add-item")]
        public async Task<IActionResult> AddItemToCart([FromBody] AddItemToCartRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // Kullanıcının belirli masadaki aktif sepetini bul veya oluştur
                var tableNumber = request.TableNumber ?? 1;
                var cart = await _context.Carts
                    .Where(c => c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                    .FirstOrDefaultAsync();

                if (cart == null)
                {
                    // Bu masa için yeni sepet oluştur
                    var cartId = Guid.NewGuid().ToString();
                    cart = new Cart
                    {
                        CartId = cartId,
                        TableNumber = tableNumber,
                        WaiterName = request.WaiterName ?? "Kasiyer",
                        UserId = userId,
                        ExpiresAt = DateTime.UtcNow.AddHours(24),
                        Status = CartStatus.Active
                    };
                    _context.Carts.Add(cart);
                    
                    _logger.LogInformation("New cart created for table {TableNumber}: {CartId} by user {UserId}", 
                        tableNumber, cartId, userId);
                }
                else
                {
                    // Bu masada mevcut sepet bulundu
                    _logger.LogInformation("Existing cart found for table {TableNumber}: {CartId}, ItemsCount={ItemsCount}", 
                        tableNumber, cart.CartId, cart.Items?.Count ?? 0);
                }

                // Ürünü kontrol et
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return NotFound(new { message = "Product not found" });
                }

                _logger.LogInformation("Processing cart item: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}", 
                    cart.CartId, request.ProductId, request.Quantity);

                // Sepette bu ürün zaten var mı kontrol et
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.ProductId == request.ProductId);

                if (existingItem != null)
                {
                    // Miktarı güncelle
                    existingItem.Quantity += request.Quantity;
                    existingItem.Notes = request.Notes ?? existingItem.Notes;
                    _logger.LogInformation("Updated existing cart item: ItemId={ItemId}, NewQuantity={NewQuantity}", 
                        existingItem.Id, existingItem.Quantity);
                }
                else
                {
                    // Yeni ürün ekle
                    var cartItem = new CartItem
                    {
                        CartId = cart.CartId,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        UnitPrice = product.Price,
                        Notes = request.Notes
                    };
                    _context.CartItems.Add(cartItem);
                    _logger.LogInformation("Added new cart item: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}", 
                        cart.CartId, request.ProductId, request.Quantity);
                }

                // Tüm değişiklikleri tek seferde kaydet
                await _context.SaveChangesAsync();
                _logger.LogInformation("All changes saved to database for cart: {CartId}", cart.CartId);

                // Güncellenmiş sepeti getir
                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                // Debug: CartItem'ları kontrol et
                _logger.LogInformation("Updated cart retrieved: CartId={CartId}, ItemsCount={ItemsCount}", 
                    updatedCart.CartId, updatedCart.Items.Count);
                
                foreach (var item in updatedCart.Items)
                {
                    _logger.LogInformation("CartItem: Id={Id}, ProductId={ProductId}, Quantity={Quantity}, UnitPrice={UnitPrice}", 
                        item.Id, item.ProductId, item.Quantity, item.UnitPrice);
                }

                var cartResponse = new CartResponse
                {
                    CartId = updatedCart.CartId,
                    TableNumber = updatedCart.TableNumber,
                    WaiterName = updatedCart.WaiterName,
                    CustomerId = updatedCart.CustomerId,
                    Notes = updatedCart.Notes,
                    Status = updatedCart.Status,
                    CreatedAt = updatedCart.CreatedAt,
                    ExpiresAt = updatedCart.ExpiresAt,
                    Items = updatedCart.Items.Select(ci => new CartItemResponse
                    {
                        Id = ci.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.Product.Name,
                        ProductImage = ci.Product.ImageUrl,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = ci.Product.TaxType.ToString(),
                        TaxRate = GetTaxRate(ci.Product.TaxType)
                    }).ToList(),
                    TotalItems = updatedCart.Items.Sum(ci => ci.Quantity),
                    Subtotal = updatedCart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = updatedCart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * GetTaxRate(ci.Product.TaxType)),
                    GrandTotal = updatedCart.Items.Sum(ci => ci.Quantity * ci.UnitPrice * (1 + GetTaxRate(ci.Product.TaxType)))
                };

                _logger.LogInformation("Item added to cart: Product {ProductId}, Quantity {Quantity}, Cart {CartId}", 
                    request.ProductId, request.Quantity, cart.CartId);

                return Ok(new { 
                    message = "Item added to cart successfully", 
                    cart = cartResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/{cartId}/items - Belirli bir sepete ürün ekle
        [HttpPost("{cartId}/items")]
        public async Task<IActionResult> AddItemToSpecificCart(string cartId, [FromBody] AddCartItemRequest request)
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

                // Sepette bu ürün zaten var mı kontrol et
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == request.ProductId);

                if (existingItem != null)
                {
                    // Miktarı güncelle
                    existingItem.Quantity += request.Quantity;
                    existingItem.Notes = request.Notes ?? existingItem.Notes;
                }
                else
                {
                    // Yeni ürün ekle
                    var cartItem = new CartItem
                    {
                        CartId = cartId,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        UnitPrice = product.Price,
                        Notes = request.Notes
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Item added to cart successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/cart/{cartId}/items/{itemId} - Sepet ürününü güncelle
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

        // DELETE: api/cart/{cartId}/items/{itemId} - Sepetten ürün çıkar
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

        // DELETE: api/cart/{cartId} - Sepeti temizle
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

        // POST: api/cart/{cartId}/clear-items - Sepet ürünlerini temizle (sepeti silme)
        [HttpPost("{cartId}/clear-items")]
        public async Task<IActionResult> ClearCartItems(string cartId)
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
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cart items cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart items");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/{cartId}/complete - Sepeti tamamla (siparişe dönüştür)
        [HttpPost("{cartId}/complete")]
        public async Task<IActionResult> CompleteCart(string cartId, [FromBody] CompleteCartRequest request)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or not active" });
                }

                if (!cart.Items.Any())
                {
                    return BadRequest(new { message = "Cannot complete empty cart" });
                }

                // Sepeti tamamlandı olarak işaretle
                cart.Status = CartStatus.Completed;
                cart.Notes = request.Notes ?? cart.Notes;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Cart completed: {CartId}", cartId);

                return Ok(new { 
                    message = "Cart completed successfully", 
                    cartId = cart.CartId,
                    totalItems = cart.Items.Sum(ci => ci.Quantity),
                    totalAmount = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing cart");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/cart/history - Kullanıcının sepet geçmişi
        [HttpGet("history")]
        public async Task<IActionResult> GetCartHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                var carts = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .Where(c => c.UserId == userId && c.Status == CartStatus.Completed)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(20)
                    .ToListAsync();

                var history = carts.Select(c => new CartHistoryResponse
                {
                    CartId = c.CartId,
                    TableNumber = c.TableNumber,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    CompletedAt = c.UpdatedAt,
                    TotalItems = c.Items.Sum(ci => ci.Quantity),
                    TotalAmount = c.Items.Sum(ci => ci.Quantity * ci.UnitPrice)
                });

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart history");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Yardımcı metod: Vergi oranını hesapla
        private decimal GetTaxRate(TaxType taxType)
        {
            return taxType switch
            {
                TaxType.Standard => 0.20m, // %20
                TaxType.Reduced => 0.10m,   // %10
                TaxType.Special => 0.13m,   // %13
                _ => 0.00m
            };
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

    public class AddItemToCartRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
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

    public class CompleteCartRequest
    {
        public string? Notes { get; set; }
    }

    public class CartResponse
    {
        public string CartId { get; set; } = string.Empty;
        public int? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Notes { get; set; }
        public CartStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<CartItemResponse> Items { get; set; } = new List<CartItemResponse>();
        public int TotalItems { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class CartItemResponse
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Notes { get; set; }
        public string TaxType { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
    }

    public class CartHistoryResponse
    {
        public string CartId { get; set; } = string.Empty;
        public int? TableNumber { get; set; }
        public CartStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
