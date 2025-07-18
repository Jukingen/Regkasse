using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;
using Registrierkasse_API.DTOs;

namespace Registrierkasse_API.Controllers
{
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
                    .Include(c => c.AppliedCoupon)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                // Türkçe açıklama: Döngüsel referansları önlemek için DTO dönülüyor.
                return Ok(ToCartDto(cart));
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
                // Kullanıcı ID'sini oturumdan (claims) al
                // Türkçe açıklama: Sepet oluşturulurken UserId, oturumdan alınır. Frontend'den gönderilmez.
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
                    CashRegisterId = Guid.TryParse(request.CashRegisterId, out var cashRegisterGuid) ? cashRegisterGuid : (Guid?)null,
                    Notes = request.Notes,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // 24 saat geçerli
                    Status = CartStatus.Active
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();

                // Cart'ı items ile birlikte yükle
                cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);

                // Eğer ilk ürün varsa ekle
                if (request.InitialItem != null)
                {
                    _logger.LogInformation("Adding initial item to cart - CartId: {CartId}, ProductId: {ProductId}, Quantity: {Quantity}", 
                        cartId, request.InitialItem.ProductId, request.InitialItem.Quantity);

                    var product = await _context.Products.FindAsync(request.InitialItem.ProductId);
                    if (product == null)
                    {
                        _logger.LogWarning("Product not found for initial item - ProductId: {ProductId}", request.InitialItem.ProductId);
                        return NotFound(new { message = "Product not found" });
                    }

                    // Stok kontrolü
                    if (product.StockQuantity < request.InitialItem.Quantity)
                    {
                        _logger.LogWarning("Insufficient stock for initial item - ProductId: {ProductId}, Requested: {Requested}, Available: {Available}", 
                            request.InitialItem.ProductId, request.InitialItem.Quantity, product.StockQuantity);
                        return BadRequest(new { message = "Insufficient stock" });
                    }

                    // İlk ürünü ekle - duplicate kontrolü yapmaya gerek yok çünkü cart yeni oluşturuluyor
                    var taxRate = GetTaxRate(product.TaxType);
                    var totalAmount = product.Price * request.InitialItem.Quantity;
                    var taxAmount = totalAmount * taxRate;
                    
                    var cartItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = cartId,
                        ProductId = request.InitialItem.ProductId,
                        ProductName = product.Name,
                        Quantity = request.InitialItem.Quantity,
                        UnitPrice = product.Price,
                        TaxRate = taxRate,
                        TaxAmount = taxAmount,
                        OriginalUnitPrice = product.Price,
                        OriginalQuantity = request.InitialItem.Quantity,
                        TotalAmount = totalAmount,
                        Notes = request.InitialItem.Notes,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.CartItems.Add(cartItem);

                    await UpdateCartTotals(cart);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Initial item added to cart - CartId: {CartId}, ItemId: {ItemId}, TotalAmount: {TotalAmount}", 
                        cartId, cartItem.Id, cart.TotalAmount);
                }

                // Cart'ı DTO olarak döndür - mevcut cart objesini kullan, tekrar veritabanından çekme
                var cartDto = ToCartDto(cart);
                return CreatedAtAction(nameof(GetCart), new { cartId }, cartDto);
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
                _logger.LogInformation("AddItemToCart called - CartId: {CartId}, ProductId: {ProductId}, Quantity: {Quantity}", 
                    cartId, request.ProductId, request.Quantity);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    _logger.LogWarning("Cart not found or expired - CartId: {CartId}", cartId);
                    return NotFound(new { message = "Cart not found or expired" });
                }

                _logger.LogInformation("Cart found - CartId: {CartId}, Items count: {ItemsCount}", cartId, cart.Items.Count);

                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found - ProductId: {ProductId}", request.ProductId);
                    return NotFound(new { message = "Product not found" });
                }

                _logger.LogInformation("Product found - ProductId: {ProductId}, Name: {ProductName}, Price: {Price}, Stock: {Stock}", 
                    request.ProductId, product.Name, product.Price, product.StockQuantity);

                // Stok kontrolü
                if (product.StockQuantity < request.Quantity)
                {
                    _logger.LogWarning("Insufficient stock - ProductId: {ProductId}, Requested: {Requested}, Available: {Available}", 
                        request.ProductId, request.Quantity, product.StockQuantity);
                    return BadRequest(new { message = "Insufficient stock" });
                }

                // Sepette zaten var mı kontrol et
                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
                if (existingItem != null)
                {
                    _logger.LogInformation("Updating existing cart item - ItemId: {ItemId}, OldQuantity: {OldQuantity}, NewQuantity: {NewQuantity}", 
                        existingItem.Id, existingItem.Quantity, existingItem.Quantity + request.Quantity);

                    // Mevcut ürünün miktarını artır
                    existingItem.Quantity += request.Quantity;
                    existingItem.TotalAmount = existingItem.Quantity * existingItem.UnitPrice;
                    existingItem.ModifiedAt = DateTime.UtcNow;
                    existingItem.IsModified = true;
                    
                    // Entity'yi güncelleme olarak işaretle
                    _context.Entry(existingItem).State = EntityState.Modified;
                }
                else
                {
                    _logger.LogInformation("Creating new cart item - ProductId: {ProductId}, Quantity: {Quantity}", 
                        request.ProductId, request.Quantity);

                    // Yeni ürün ekle
                    var taxRate = GetTaxRate(product.TaxType);
                    var totalAmount = product.Price * request.Quantity;
                    var taxAmount = totalAmount * taxRate;
                    
                    var cartItem = new CartItem
                    {
                        Id = Guid.NewGuid(), // Manuel olarak ID set et
                        CartId = cartId,
                        ProductId = request.ProductId,
                        ProductName = product.Name, // Product name'i de set et
                        Quantity = request.Quantity,
                        UnitPrice = product.Price,
                        TaxRate = taxRate, // Vergi oranını hesapla
                        TaxAmount = taxAmount,
                        OriginalUnitPrice = product.Price,
                        OriginalQuantity = request.Quantity,
                        TotalAmount = totalAmount,
                        Notes = request.Notes,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _logger.LogInformation("CartItem created - ItemId: {ItemId}, TotalAmount: {TotalAmount}", 
                        cartItem.Id, cartItem.TotalAmount);

                    // Yeni entity'yi hem context'e hem de cart.Items'a ekle
                    _context.CartItems.Add(cartItem);
                    cart.Items.Add(cartItem); // Bu satır eksikti!
                }

                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cart totals updated and saved - CartId: {CartId}, TotalAmount: {TotalAmount}", 
                    cartId, cart.TotalAmount);

                // Güncellenmiş cart'ı DTO olarak döndür
                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);

                var cartDto = ToCartDto(updatedCart);
                _logger.LogInformation("Returning updated cart - CartId: {CartId}, Items count: {ItemsCount}, TotalAmount: {TotalAmount}", 
                    cartId, cartDto.Items.Count, cartDto.TotalAmount);

                return Ok(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/cart/{cartId}/items/{itemId}
        [HttpPut("{cartId}/items/{itemId}")]
        public async Task<IActionResult> UpdateCartItem(string cartId, Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                var cartItem = cart.Items.FirstOrDefault(i => i.Id == itemId);
                if (cartItem == null)
                {
                    return NotFound(new { message = "Cart item not found" });
                }

                // Stok kontrolü
                var product = await _context.Products.FindAsync(cartItem.ProductId);
                if (product != null && product.StockQuantity < request.Quantity)
                {
                    return BadRequest(new { message = "Insufficient stock" });
                }

                cartItem.Quantity = request.Quantity;
                cartItem.UnitPrice = request.UnitPrice;
                cartItem.TotalAmount = request.Quantity * request.UnitPrice;
                cartItem.Notes = request.Notes;
                cartItem.ModifiedAt = DateTime.UtcNow;
                cartItem.IsModified = true;

                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);
                return Ok(ToCartDto(updatedCart));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item {ItemId}", itemId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/cart/{cartId}/items/{itemId}
        [HttpDelete("{cartId}/items/{itemId}")]
        public async Task<IActionResult> RemoveCartItem(string cartId, Guid itemId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                var cartItem = cart.Items.FirstOrDefault(i => i.Id == itemId);
                if (cartItem == null)
                {
                    return NotFound(new { message = "Cart item not found" });
                }

                cart.Items.Remove(cartItem);
                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);
                return Ok(ToCartDto(updatedCart));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item {ItemId}", itemId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/{cartId}/apply-coupon
        [HttpPost("{cartId}/apply-coupon")]
        public async Task<IActionResult> ApplyCoupon(string cartId, [FromBody] ApplyCouponRequest request)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                var coupon = await _context.Coupons
                    .FirstOrDefaultAsync(c => c.Code == request.CouponCode && c.IsActive);

                if (coupon == null)
                {
                    return BadRequest(new { message = "Invalid coupon code" });
                }

                // Kupon geçerlilik kontrolü
                if (coupon.ValidFrom > DateTime.UtcNow || coupon.ValidUntil < DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Coupon is not valid" });
                }

                // Minimum tutar kontrolü
                if (cart.TotalAmount < coupon.MinimumAmount)
                {
                    return BadRequest(new { message = $"Minimum amount required: €{coupon.MinimumAmount}" });
                }

                // Müşteri kategorisi kontrolü
                if (cart.Customer != null && coupon.CustomerCategoryRestriction.HasValue)
                {
                    if (cart.Customer.Category != coupon.CustomerCategoryRestriction.Value)
                    {
                        return BadRequest(new { message = "Coupon not applicable for this customer category" });
                    }
                }

                cart.AppliedCouponId = coupon.Id;
                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying coupon to cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/cart/{cartId}/remove-coupon
        [HttpDelete("{cartId}/remove-coupon")]
        public async Task<IActionResult> RemoveCoupon(string cartId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                cart.AppliedCouponId = null;
                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                return Ok(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing coupon from cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/cart/{cartId}
        [HttpDelete("{cartId}")]
        public async Task<IActionResult> ClearCart(string cartId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                // CartItem'ları veritabanından sil
                _context.CartItems.RemoveRange(cart.Items);
                cart.AppliedCouponId = null;
                await UpdateCartTotals(cart);
                await _context.SaveChangesAsync();

                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);
                return Ok(ToCartDto(updatedCart));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/{cartId}/complete
        [HttpPost("{cartId}/complete")]
        public async Task<IActionResult> CompleteCart(string cartId, [FromBody] CompleteCartRequest request)
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

                if (!cart.Items.Any())
                {
                    return BadRequest(new { message = "Cart is empty" });
                }

                // Stok kontrolü ve güncelleme
                foreach (var item in cart.Items)
                {
                    var product = item.Product;
                    if (product.StockQuantity < item.Quantity)
                    {
                        return BadRequest(new { message = $"Insufficient stock for {product.Name}" });
                    }

                    product.StockQuantity -= item.Quantity;
                }

                // Sepeti tamamlandı olarak işaretle
                cart.Status = CartStatus.Completed;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Cart completed successfully", cartId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing cart {CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private async Task UpdateCartTotals(Cart cart)
        {
            // Cart'ın items'ını yeniden yükle
            var cartItems = await _context.CartItems
                .Where(ci => ci.CartId == cart.CartId && ci.IsActive)
                .ToListAsync();

            cart.Subtotal = cartItems.Sum(i => i.TotalAmount);
            cart.TaxAmount = cartItems.Sum(i => i.TaxAmount);
            cart.DiscountAmount = 0;

            // Kupon indirimi hesapla
            if (cart.AppliedCouponId.HasValue)
            {
                var coupon = await _context.Coupons.FindAsync(cart.AppliedCouponId.Value);
                if (coupon != null)
                {
                    if (coupon.DiscountType == DiscountType.Percentage)
                    {
                        cart.DiscountAmount = cart.Subtotal * (coupon.DiscountValue / 100);
                    }
                    else
                    {
                        cart.DiscountAmount = coupon.DiscountValue;
                    }
                }
            }

            cart.TotalAmount = cart.Subtotal + cart.TaxAmount - cart.DiscountAmount;
        }

        // Vergi oranını hesapla
        private decimal GetTaxRate(TaxType taxType)
        {
            return taxType switch
            {
                TaxType.Reduced => 0.10m,
                TaxType.Special => 0.13m,
                TaxType.Standard => 0.20m,
                _ => 0.20m // default
            };
        }

        // Yardımcı fonksiyon: Entity'den DTO'ya dönüştür
        private CartDto ToCartDto(Cart cart)
        {
            return new CartDto
            {
                CartId = cart.CartId,
                Items = cart.Items.Select(i => new CartItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Product = new ProductDto
                    {
                        Id = i.ProductId,
                        Name = i.ProductName,
                        Description = i.Product?.Description ?? "",
                        Price = i.UnitPrice,
                        StockQuantity = i.Product?.StockQuantity ?? 0,
                        Unit = i.Product?.Unit ?? "",
                        Category = i.Product?.Category ?? "",
                        TaxType = i.Product?.TaxType.ToString() ?? "Standard",
                        IsActive = i.Product?.IsActive ?? true
                    },
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalAmount = i.TotalAmount,
                    TaxRate = i.TaxRate,
                    DiscountAmount = i.DiscountAmount,
                    Notes = i.Notes
                }).ToList(),
                Subtotal = cart.Subtotal,
                TaxAmount = cart.TaxAmount,
                DiscountAmount = cart.DiscountAmount,
                TotalAmount = cart.TotalAmount,
                Notes = cart.Notes,
                Status = cart.Status.ToString(),
                ExpiresAt = cart.ExpiresAt
            };
        }
    }

    // Request models
    public class CreateCartRequest
    {
        public string? TableNumber { get; set; }
        public string? WaiterName { get; set; }
        public string? CustomerId { get; set; }
        public string? CashRegisterId { get; set; }
        public string? Notes { get; set; }
        public AddCartItemRequest? InitialItem { get; set; }
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
        public decimal UnitPrice { get; set; }
        public string? Notes { get; set; }
    }

    public class ApplyCouponRequest
    {
        public string CouponCode { get; set; } = string.Empty;
    }

    public class CompleteCartRequest
    {
        public string PaymentMethod { get; set; } = "cash";
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
    }
} 