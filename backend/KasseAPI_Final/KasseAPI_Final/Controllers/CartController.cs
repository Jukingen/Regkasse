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

                // 🔍 DEBUG: Masa numarasını logla
                _logger.LogInformation("GetCurrentUserCart called - TableNumber: {TableNumber}, UserId: {UserId}", tableNumber, userId);

                // 🔒 GÜVENLİK: SADECE KULLANICININ KENDİ SEPETİNİ GÖRMESİ
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .Include(c => c.Customer)
                    .Where(c => c.TableNumber == tableNumber && 
                               c.Status == CartStatus.Active &&
                               c.UserId == userId) // 🔒 Güvenlik: UserId kontrolü eklendi
                    .FirstOrDefaultAsync();

                if (cart == null)
                {
                    // 🔍 DEBUG: Bu masa için hangi sepetler var kontrol et (sadece debug için)
                    var allCartsForTable = await _context.Carts
                        .Where(c => c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                        .Select(c => new { c.CartId, c.UserId, c.CreatedAt, c.Items.Count })
                        .ToListAsync();
                    
                    _logger.LogInformation("No cart found for user {UserId} at table {TableNumber}. Available carts: {Carts}", 
                        userId, tableNumber, 
                        string.Join(", ", allCartsForTable.Select(c => $"CartId={c.CartId}, UserId={c.UserId}, Items={c.Count}")));
                    
                    // 🆕 YENİ ÖZELLİK: Kullanıcının o masada sepeti yoksa otomatik oluştur
                    _logger.LogInformation("Creating new cart for user {UserId} at table {TableNumber}", userId, tableNumber);
                    
                    var newCart = new Cart
                    {
                        CartId = Guid.NewGuid().ToString(),
                        TableNumber = tableNumber,
                        WaiterName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Kasiyer",
                        UserId = userId,
                        ExpiresAt = DateTime.UtcNow.AddHours(24),
                        Status = CartStatus.Active,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    _context.Carts.Add(newCart);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("New cart created: {CartId} for user {UserId} at table {TableNumber}", 
                        newCart.CartId, userId, tableNumber);
                    
                    // Yeni oluşturulan sepeti döndür
                    var emptyCartResponse = new CartResponse
                    {
                        CartId = newCart.CartId,
                        TableNumber = newCart.TableNumber,
                        WaiterName = newCart.WaiterName,
                        CustomerId = newCart.CustomerId,
                        Notes = newCart.Notes,
                        Status = newCart.Status,
                        CreatedAt = newCart.CreatedAt,
                        ExpiresAt = newCart.ExpiresAt,
                        Items = new List<CartItemResponse>(), // Boş sepet
                        TotalItems = 0,
                        Subtotal = 0,
                        TotalTax = 0,
                        GrandTotal = 0
                    };
                    
                    return Ok(emptyCartResponse);
                }

                // Debug: Cart ve CartItem'ları kontrol et
                _logger.LogInformation("GetCurrentUserCart: Table={TableNumber}, CartId={CartId}, ItemsCount={ItemsCount}, CreatedAt={CreatedAt}", 
                    tableNumber, cart.CartId, cart.Items.Count, cart.CreatedAt);
                
                // 🔍 DEBUG: Tüm cart'ları listele (sadece debug için)
                var allCarts = await _context.Carts
                    .Where(c => c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                    .Select(c => new { c.CartId, c.UserId, c.CreatedAt, c.Items.Count })
                    .ToListAsync();
                
                _logger.LogInformation("All carts for table {TableNumber} (debug): {Carts}", tableNumber, 
                    string.Join(", ", allCarts.Select(c => $"CartId={c.CartId}, UserId={c.UserId}, Items={c.Count}")));
                
                foreach (var item in cart.Items)
                {
                    _logger.LogInformation("CartItem in GetCurrentUserCart: Id={Id}, ProductId={ProductId}, Quantity={Quantity}", 
                        item.Id, item.ProductId, item.Quantity);
                }

                // Product bilgilerini ayrı sorgu ile al
                var productIds = cart.Items.Select(ci => ci.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

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
                        ProductName = products.TryGetValue(ci.ProductId, out var product) ? product.Name : "Unknown Product",
                        ProductImage = products.TryGetValue(ci.ProductId, out var p) ? p.ImageUrl : null,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = products.TryGetValue(ci.ProductId, out var prod) ? prod.TaxType : "Standard",
                        TaxRate = products.TryGetValue(ci.ProductId, out var pr) ? GetTaxRate(pr.TaxType) : 0.20m
                    }).ToList(),
                    TotalItems = cart.Items.Sum(ci => ci.Quantity),
                    Subtotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = cart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (product != null ? GetTaxRate(product.TaxType) : 0.20m);
                    }),
                    GrandTotal = cart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (1 + (product != null ? GetTaxRate(product.TaxType) : 0.20m));
                    })
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
                // ✅ Token'dan kullanıcı ID'sini al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Cart'ın bu kullanıcıya ait olduğunu kontrol et
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId && c.Status == CartStatus.Active);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found or expired" });
                }

                // Product bilgilerini ayrı sorgu ile al
                var productIds = cart.Items.Select(ci => ci.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

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
                        ProductName = products.TryGetValue(ci.ProductId, out var product) ? product.Name : "Unknown Product",
                        ProductImage = products.TryGetValue(ci.ProductId, out var p) ? p.ImageUrl : null,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = products.TryGetValue(ci.ProductId, out var prod) ? prod.TaxType : "Standard",
                        TaxRate = products.TryGetValue(ci.ProductId, out var pr) ? GetTaxRate(pr.TaxType) : 0.20m
                    }).ToList(),
                    TotalItems = cart.Items.Sum(ci => ci.Quantity),
                    Subtotal = cart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = cart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (product != null ? GetTaxRate(product.TaxType) : 0.20m);
                    }),
                    GrandTotal = cart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (1 + (product != null ? GetTaxRate(product.TaxType) : 0.20m));
                    })
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
                
                // ✅ GÜVENLİK: SADECE KULLANICININ KENDİ SEPETİNİ GÖRMESİ
                var cart = await _context.Carts
                    .Where(c => c.TableNumber == tableNumber && 
                               c.Status == CartStatus.Active && 
                               c.UserId == userId) // 🔒 Güvenlik: UserId kontrolü
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
                        UserId = userId, // 🔒 Güvenlik: UserId set et
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
                    _logger.LogInformation("Existing cart found for table {TableNumber}: {CartId}, ItemsCount={ItemsCount}, UserId={UserId}", 
                        tableNumber, cart.CartId, cart.Items?.Count ?? 0, cart.UserId);
                }

                // Ürünü kontrol et
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return NotFound(new { message = "Product not found" });
                }

                _logger.LogInformation("Processing cart item: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}, UserId={UserId}", 
                    cart.CartId, request.ProductId, request.Quantity, userId);

                // Sepette bu ürün zaten var mı kontrol et
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.ProductId == request.ProductId);

                if (existingItem != null)
                {
                    // Miktarı güncelle
                    existingItem.Quantity += request.Quantity;
                    existingItem.Notes = request.Notes ?? existingItem.Notes;
                    _logger.LogInformation("Updated existing cart item: ItemId={ItemId}, NewQuantity={NewQuantity}, UserId={UserId}", 
                        existingItem.Id, existingItem.Quantity, userId);
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
                    _logger.LogInformation("Added new cart item: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}, UserId={UserId}", 
                        cart.CartId, request.ProductId, request.Quantity, userId);
                }

                // Tüm değişiklikleri tek seferde kaydet
                await _context.SaveChangesAsync();
                _logger.LogInformation("All changes saved to database for cart: {CartId}, UserId: {UserId}", cart.CartId, userId);

                // Güncellenmiş sepeti getir
                var updatedCart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                // Debug: CartItem'ları kontrol et
                _logger.LogInformation("Updated cart retrieved: CartId={CartId}, ItemsCount={ItemsCount}, UserId={UserId}", 
                    updatedCart.CartId, updatedCart.Items.Count, userId);
                
                foreach (var item in updatedCart.Items)
                {
                    _logger.LogInformation("CartItem: Id={Id}, ProductId={ProductId}, Quantity={Quantity}, UnitPrice={UnitPrice}", 
                        item.Id, item.ProductId, item.Quantity, item.UnitPrice);
                }

                // Product bilgilerini ayrı sorgu ile al
                var productIds = updatedCart.Items.Select(ci => ci.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

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
                        ProductName = products.TryGetValue(ci.ProductId, out var product) ? product.Name : "Unknown Product",
                        ProductImage = products.TryGetValue(ci.ProductId, out var p) ? p.ImageUrl : null,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice,
                        TotalPrice = ci.Quantity * ci.UnitPrice,
                        Notes = ci.Notes,
                        TaxType = products.TryGetValue(ci.ProductId, out var prod) ? prod.TaxType : "Standard",
                        TaxRate = products.TryGetValue(ci.ProductId, out var pr) ? GetTaxRate(pr.TaxType) : 0.20m
                    }).ToList(),
                    TotalItems = updatedCart.Items.Sum(ci => ci.Quantity),
                    Subtotal = updatedCart.Items.Sum(ci => ci.Quantity * ci.UnitPrice),
                    TotalTax = updatedCart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (product != null ? GetTaxRate(product.TaxType) : 0.20m);
                    }),
                    GrandTotal = updatedCart.Items.Sum(ci => {
                        var product = products.TryGetValue(ci.ProductId, out var p) ? p : null;
                        return ci.Quantity * ci.UnitPrice * (1 + (product != null ? GetTaxRate(product.TaxType) : 0.20m));
                    })
                };

                _logger.LogInformation("Item added to cart: Product {ProductId}, Quantity {Quantity}, Cart {CartId}, UserId: {UserId}", 
                    request.ProductId, request.Quantity, cart.CartId, userId);

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
                // ✅ Token'dan kullanıcı ID'sini al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Cart'ın bu kullanıcıya ait olduğunu kontrol et
                var cart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId && c.Status == CartStatus.Active);
                
                if (cart == null)
                    return NotFound(new { message = $"Cart {cartId} not found or not accessible" });

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

        // PUT: api/cart/items/{itemId} - Sepet ürününü güncelle (basit versiyon)
        [HttpPut("items/{itemId}")]
        public async Task<IActionResult> UpdateCartItemSimple(Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("UpdateCartItemSimple called - ItemId: {ItemId}, Quantity: {Quantity}, UserId: {UserId}", 
                    itemId, request.Quantity, userId);

                // CartItem'ı bul ve kullanıcı doğrulaması yap
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId);

                if (cartItem == null)
                {
                    _logger.LogWarning("CartItem not found - ItemId: {ItemId}", itemId);
                    return NotFound(new { message = "Cart item not found" });
                }

                // Kullanıcının bu cart'a erişim yetkisi var mı kontrol et
                if (cartItem.Cart.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} does not have access to cart {CartId}", userId, cartItem.Cart.CartId);
                    return Forbid();
                }

                // Ürün miktarını güncelle
                cartItem.Quantity = request.Quantity;
                cartItem.Notes = request.Notes ?? cartItem.Notes;

                await _context.SaveChangesAsync();

                _logger.LogInformation("CartItem updated successfully - ItemId: {ItemId}, NewQuantity: {Quantity}", itemId, request.Quantity);

                return Ok(new { message = "Cart item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item - ItemId: {ItemId}", itemId);
                return StatusCode(500, new { message = "Internal server error while updating cart item" });
            }
        }

        // PUT: api/cart/{cartId}/items/{itemId} - Sepet ürününü güncelle (cartId ile)
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
                // ✅ Token'dan kullanıcı ID'sini al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Cart'ın bu kullanıcıya ait olduğunu kontrol et
                var cart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);
                
                if (cart == null)
                    return NotFound(new { message = $"Cart {cartId} not found or not accessible" });

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
                // ✅ Token'dan kullanıcı ID'sini al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Cart'ın bu kullanıcıya ait olduğunu kontrol et
                var cart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);
                
                if (cart == null)
                    return NotFound(new { message = $"Cart {cartId} not found or not accessible" });

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
                // ✅ Token'dan kullanıcı ID'sini al
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                // ✅ Cart'ın bu kullanıcıya ait olduğunu kontrol et
                var cart = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);
                
                if (cart == null)
                    return NotFound(new { message = $"Cart {cartId} not found or not accessible" });

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

        // POST: api/cart/clear - Belirli masadaki sepeti temizle
        [HttpPost("clear")]
        public async Task<IActionResult> ClearCartByTable([FromQuery] int tableNumber)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("🧹 Clearing cart for table: TableNumber={TableNumber}, UserId={UserId}", 
                    tableNumber, userId);

                // ✅ GÜVENLİK: SADECE KULLANICININ KENDİ SEPETİNİ TEMİZLEMESİ
                var carts = await _context.Carts
                    .Include(c => c.Items)
                    .Where(c => c.TableNumber == tableNumber && 
                               c.Status == CartStatus.Active && 
                               c.UserId == userId) // 🔒 Güvenlik: UserId kontrolü
                    .ToListAsync();

                if (!carts.Any())
                {
                    _logger.LogInformation("ℹ️ No active carts found for user {UserId} at table: TableNumber={TableNumber}", 
                        userId, tableNumber);
                    return Ok(new { message = $"No active carts found for table {tableNumber}", clearedCount = 0 });
                }

                // 🔍 DEBUG: Bu masa için hangi sepetler var kontrol et (sadece debug için)
                var allCartsForTable = await _context.Carts
                    .Where(c => c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                    .Select(c => new { c.CartId, c.UserId, c.CreatedAt, c.Items.Count })
                    .ToListAsync();
                
                _logger.LogInformation("🔍 All carts for table {TableNumber} (debug): {Carts}", tableNumber, 
                    string.Join(", ", allCartsForTable.Select(c => $"CartId={c.CartId}, UserId={c.UserId}, Items={c.Count}")));

                int totalItemsCleared = 0;
                int totalCartsCleared = 0;

                foreach (var cart in carts)
                {
                    // Sepet ürünlerini temizle
                    if (cart.Items.Any())
                    {
                        totalItemsCleared += cart.Items.Count;
                        _context.CartItems.RemoveRange(cart.Items);
                        _logger.LogInformation("🧹 Cleared {ItemsCount} items from cart: CartId={CartId}, TableNumber={TableNumber}, UserId={UserId}", 
                            cart.Items.Count, cart.CartId, tableNumber, cart.UserId);
                    }

                    // Sepeti sil
                    _context.Carts.Remove(cart);
                    totalCartsCleared++;
                    _logger.LogInformation("🗑️ Removed cart: CartId={CartId}, TableNumber={TableNumber}, UserId={UserId}", 
                        cart.CartId, tableNumber, cart.UserId);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Successfully cleared {CartsCount} carts with {ItemsCount} total items for user {UserId} at table: TableNumber={TableNumber}", 
                    totalCartsCleared, totalItemsCleared, userId, tableNumber);

                return Ok(new { 
                    message = $"Successfully cleared {totalCartsCleared} carts with {totalItemsCleared} total items for table {tableNumber}",
                    clearedCarts = totalCartsCleared,
                    clearedItems = totalItemsCleared,
                    tableNumber = tableNumber,
                    userId = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error clearing cart for table: TableNumber={TableNumber}", tableNumber);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/cart/clear-all - Kullanıcının tüm masalarındaki sepetleri temizle
        [HttpPost("clear-all")]
        public async Task<IActionResult> ClearAllUserCarts()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("🧹 CLEAR ALL CARTS requested by UserId: {UserId}, Role: {UserRole}", userId, userRole);

                // ✅ GÜVENLİK: SADECE KULLANICININ KENDİ TÜM SEPETLERİNİ TEMİZLEME
                var allUserCarts = await _context.Carts
                    .Include(c => c.Items)
                    .Where(c => c.Status == CartStatus.Active && c.UserId == userId) // 🔒 Sadece şu anki kullanıcının sepetleri
                    .ToListAsync();

                if (!allUserCarts.Any())
                {
                    _logger.LogInformation("ℹ️ No active carts found for user {UserId}", userId);
                    return Ok(new { 
                        message = "No active carts found to clear", 
                        clearedCarts = 0,
                        clearedItems = 0,
                        affectedTables = new List<int>()
                    });
                }

                var clearedTablesInfo = new List<object>();
                int totalItemsCleared = 0;
                int totalCartsCleared = 0;

                foreach (var cart in allUserCarts)
                {
                    var itemsCount = cart.Items.Count;
                    totalItemsCleared += itemsCount;
                    totalCartsCleared++;

                    clearedTablesInfo.Add(new {
                        tableNumber = cart.TableNumber,
                        cartId = cart.CartId,
                        itemsCount = itemsCount,
                        clearedAt = DateTime.UtcNow
                    });

                    // Sepet ürünlerini sil
                    if (cart.Items.Any())
                    {
                        _context.CartItems.RemoveRange(cart.Items);
                        _logger.LogInformation("🧹 Cleared {ItemsCount} items from table {TableNumber}, CartId: {CartId}, UserId: {UserId}", 
                            itemsCount, cart.TableNumber, cart.CartId, userId);
                    }

                    // Sepeti sil
                    _context.Carts.Remove(cart);
                    _logger.LogInformation("🗑️ Removed cart from table {TableNumber}, CartId: {CartId}, UserId: {UserId}", 
                        cart.TableNumber, cart.CartId, userId);
                }

                // Değişiklikleri kaydet
                await _context.SaveChangesAsync();

                var affectedTables = allUserCarts.Select(c => c.TableNumber).Distinct().OrderBy(t => t).ToList();

                _logger.LogInformation("✅ ALL CARTS CLEARED: {CartsCount} carts, {ItemsCount} items from {TablesCount} tables by UserId: {UserId}. Affected tables: {Tables}", 
                    totalCartsCleared, totalItemsCleared, affectedTables.Count, userId, string.Join(", ", affectedTables));

                return Ok(new { 
                    message = $"Successfully cleared all carts: {totalCartsCleared} carts with {totalItemsCleared} items from {affectedTables.Count} tables",
                    clearedCarts = totalCartsCleared,
                    clearedItems = totalItemsCleared,
                    affectedTables = affectedTables,
                    clearedTablesDetails = clearedTablesInfo,
                    userId = userId,
                    clearedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error clearing all carts for user");
                return StatusCode(500, new { message = "Internal server error during clear all operation" });
            }
        }

        // POST: api/cart/{cartId}/reset-after-payment - Ödeme sonrası sepeti sıfırla ve yeni sipariş durumunu güncelle
        [HttpPost("{cartId}/reset-after-payment")]
        public async Task<IActionResult> ResetCartAfterPayment(string cartId, [FromBody] ResetCartAfterPaymentRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Resetting cart after payment: CartId={CartId}, UserId={UserId}, UserRole={UserRole}", 
                    cartId, userId, userRole);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cartId);

                if (cart == null)
                {
                    _logger.LogWarning("Cart not found for reset after payment: CartId={CartId}", cartId);
                    return NotFound(new { message = "Cart not found" });
                }

                // Sepet durumunu "Completed" olarak güncelle
                cart.Status = CartStatus.Completed;
                cart.UpdatedAt = DateTime.UtcNow;
                cart.Notes = request.Notes ?? cart.Notes;

                // Sepet ürünlerini temizle
                var cartItems = await _context.CartItems
                    .Where(ci => ci.CartId == cartId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);

                // Yeni boş sepet oluştur (aynı masa için)
                var newCart = new Cart
                {
                    CartId = Guid.NewGuid().ToString(),
                    TableNumber = cart.TableNumber,
                    WaiterName = cart.WaiterName,
                    CustomerId = null, // Yeni müşteri için
                    Status = CartStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Notes = "New order after payment completion",
                    UserId = userId
                };

                _context.Carts.Add(newCart);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Cart reset after payment completed: OldCartId={OldCartId}, NewCartId={NewCartId}, TableNumber={TableNumber}", 
                    cartId, newCart.CartId, cart.TableNumber);

                return Ok(new { 
                    message = "Cart reset after payment completed successfully",
                    oldCartId = cartId,
                    newCartId = newCart.CartId,
                    tableNumber = cart.TableNumber,
                    status = "ready_for_new_order"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting cart after payment: CartId={CartId}", cartId);
                return StatusCode(500, new { message = "Internal server error during cart reset" });
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

        // DELETE: api/cart/items/{itemId} - Sepetten ürün sil
        [HttpDelete("items/{itemId}")]
        public async Task<IActionResult> RemoveCartItem(Guid itemId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("RemoveCartItem called - ItemId: {ItemId}, UserId: {UserId}", itemId, userId);

                // CartItem'ı bul
                var cartItem = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefaultAsync(ci => ci.Id == itemId);

                if (cartItem == null)
                {
                    _logger.LogWarning("CartItem not found - ItemId: {ItemId}", itemId);
                    return NotFound(new { message = "Cart item not found" });
                }

                // Kullanıcının bu cart'a erişim yetkisi var mı kontrol et
                if (cartItem.Cart.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} does not have access to cart {CartId}", userId, cartItem.Cart.CartId);
                    return Forbid();
                }

                // CartItem'ı sil
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation("CartItem removed successfully - ItemId: {ItemId}, CartId: {CartId}", itemId, cartItem.Cart.CartId);

                return Ok(new { message = "Cart item removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item - ItemId: {ItemId}", itemId);
                return StatusCode(500, new { message = "Internal server error while removing cart item" });
            }
        }

        // POST: api/cart/force-cleanup - Manuel sepet temizleme (admin/kasiyer için)
        [HttpPost("force-cleanup")]
        public async Task<IActionResult> ForceCartCleanup([FromBody] ForceCleanupRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("🧹 Force cart cleanup requested by UserId: {UserId}, Role: {UserRole}, Reason: {Reason}", 
                    userId, userRole, request.Reason);

                // 🔒 GÜVENLİK: Sadece admin ve kasiyerler manuel temizlik yapabilir
                if (userRole != "Admin" && userRole != "Kasiyer")
                {
                    _logger.LogWarning("⚠️ Unauthorized force cleanup attempt by UserId: {UserId}, Role: {UserRole}", userId, userRole);
                    return Forbid();
                }

                var cleanupResult = new ForceCleanupResponse
                {
                    RequestedBy = userId,
                    RequestedAt = DateTime.UtcNow,
                    Reason = request.Reason,
                    CleanedCarts = new List<CleanedCartInfo>(),
                    TotalItemsCleaned = 0,
                    TotalCartsCleared = 0
                };

                // Belirli masa için temizlik
                if (request.TableNumber.HasValue)
                {
                    var tableCarts = await _context.Carts
                        .Include(c => c.Items)
                        .Where(c => c.TableNumber == request.TableNumber.Value && 
                                   c.Status == CartStatus.Active)
                        .ToListAsync();

                    foreach (var cart in tableCarts)
                    {
                        var itemsCount = cart.Items.Count;
                        cleanupResult.TotalItemsCleaned += itemsCount;
                        cleanupResult.TotalCartsCleared++;

                        cleanupResult.CleanedCarts.Add(new CleanedCartInfo
                        {
                            CartId = cart.CartId,
                            TableNumber = cart.TableNumber,
                            UserId = cart.UserId,
                            ItemsCount = itemsCount,
                            CreatedAt = cart.CreatedAt
                        });

                        // Sepet ürünlerini sil
                        _context.CartItems.RemoveRange(cart.Items);
                        // Sepeti sil
                        _context.Carts.Remove(cart);

                        _logger.LogInformation("🧹 Force cleanup: Cart {CartId} at table {TableNumber} cleaned (Items: {ItemsCount})", 
                            cart.CartId, cart.TableNumber, itemsCount);
                    }
                }
                // Tüm sepetler için temizlik
                else
                {
                    var allActiveCarts = await _context.Carts
                        .Include(c => c.Items)
                        .Where(c => c.Status == CartStatus.Active)
                        .ToListAsync();

                    foreach (var cart in allActiveCarts)
                    {
                        var itemsCount = cart.Items.Count;
                        cleanupResult.TotalItemsCleaned += itemsCount;
                        cleanupResult.TotalCartsCleared++;

                        cleanupResult.CleanedCarts.Add(new CleanedCartInfo
                        {
                            CartId = cart.CartId,
                            TableNumber = cart.TableNumber,
                            UserId = cart.UserId,
                            ItemsCount = itemsCount,
                            CreatedAt = cart.CreatedAt
                        });

                        // Sepet ürünlerini sil
                        _context.CartItems.RemoveRange(cart.Items);
                        // Sepeti sil
                        _context.Carts.Remove(cart);

                        _logger.LogInformation("🧹 Force cleanup: Cart {CartId} at table {TableNumber} cleaned (Items: {ItemsCount})", 
                            cart.CartId, cart.TableNumber, itemsCount);
                    }
                }

                // Değişiklikleri kaydet
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Force cart cleanup completed: {CartsCount} carts, {ItemsCount} items cleaned by UserId: {UserId}", 
                    cleanupResult.TotalCartsCleared, cleanupResult.TotalItemsCleaned, userId);

                return Ok(cleanupResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during force cart cleanup");
                return StatusCode(500, new { message = "Internal server error during force cleanup" });
            }
        }

        // GET: api/cart/table-orders-recovery - F5 sonrası masa siparişlerini geri yükleme
        [HttpGet("table-orders-recovery")]
        public async Task<IActionResult> GetTableOrdersForRecovery()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("🔄 Table orders recovery requested by user {UserId}", userId);

                                               // Kullanıcının tüm aktif masa siparişlerini getir - RKSV uyumlu güvenlik kontrolü
                // Önce TableOrder'lardan kontrol et, yoksa Cart'tan al
                var userActiveTableOrders = await _context.TableOrders
                    .Include(to => to.Items)
                    // .ThenInclude(toi => toi.Product) // Temporarily disabled due to CategoryId1 conflict
                    .Include(to => to.Customer)
                    .Include(to => to.User) // User navigation property'yi ekle
                    .Where(to => to.UserId == userId &&
                                (to.Status == TableOrderStatus.Active ||
                                 to.Status == TableOrderStatus.Preparing ||
                                 to.Status == TableOrderStatus.Ready))
                    .OrderBy(to => to.TableNumber)
                    .ToListAsync();

                // Eğer TableOrder yoksa Cart'tan al (backward compatibility)
                var userActiveCarts = await _context.Carts
                    .Include(c => c.Items)
                    // .ThenInclude(ci => ci.Product) // Temporarily disabled due to CategoryId1 conflict
                    .Include(c => c.Customer)
                    .Include(c => c.User) // User navigation property'yi ekle
                    .Where(c => c.UserId == userId && c.Status == CartStatus.Active)
                    .OrderBy(c => c.TableNumber)
                    .ToListAsync();

                // TableOrder'ları öncelikle kullan
                var tableOrderResponses = userActiveTableOrders.Select(tableOrder => new TableOrderRecoveryResponse
                {
                    TableNumber = tableOrder.TableNumber,
                    CartId = tableOrder.TableOrderId, // TableOrderId'yi CartId olarak kullan
                    CustomerName = tableOrder.CustomerName ?? tableOrder.WaiterName,
                    ItemCount = tableOrder.Items?.Count ?? 0,
                    TotalAmount = tableOrder.TotalAmount,
                    Status = tableOrder.Status.ToString(),
                    CreatedAt = tableOrder.OrderStartTime,
                    LastUpdated = tableOrder.LastModifiedTime ?? tableOrder.OrderStartTime,
                    Items = tableOrder.Items?.Select(item => new TableOrderItemInfo
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = item.UnitPrice,
                        Total = item.TotalPrice,
                        Notes = item.Notes
                    }).ToList() ?? new List<TableOrderItemInfo>()
                }).ToList();

                // Cart'lardan gelen siparişleri ekle (TableOrder yoksa)
                var cartBasedOrders = userActiveCarts
                    .Where(cart => !userActiveTableOrders.Any(to => to.TableNumber == cart.TableNumber))
                    .Select(cart => new TableOrderRecoveryResponse
                    {
                        TableNumber = cart.TableNumber,
                        CartId = cart.CartId,
                        CustomerName = cart.Customer?.Name ?? cart.WaiterName,
                        ItemCount = cart.Items?.Count ?? 0,
                        TotalAmount = cart.Items?.Sum(i => i.UnitPrice * i.Quantity) ?? 0,
                        Status = cart.Status.ToString(),
                        CreatedAt = cart.CreatedAt,
                        LastUpdated = cart.UpdatedAt ?? cart.CreatedAt,
                        Items = cart.Items?.Select(item => new TableOrderItemInfo
                        {
                            ProductId = item.ProductId,
                            ProductName = "Product", // Temporarily hardcoded due to CategoryId1 conflict
                            Quantity = item.Quantity,
                            Price = item.UnitPrice,
                            Total = item.UnitPrice * item.Quantity,
                            Notes = item.Notes
                        }).ToList() ?? new List<TableOrderItemInfo>()
                    });

                // Tüm siparişleri birleştir
                var allTableOrders = tableOrderResponses.Concat(cartBasedOrders).ToList();

                // İngilizce teknik log - RKSV uyumlu
                _logger.LogInformation("Table orders recovery completed for user {UserId}: {TableOrderCount} TableOrders + {CartCount} Cart-based orders = {TotalCount} total active table orders", 
                    userId, tableOrderResponses.Count, cartBasedOrders.Count(), allTableOrders.Count);

                return Ok(new
                {
                    success = true,
                    message = "Table orders retrieved successfully for page refresh recovery (TableOrder + Cart hybrid)",
                    userId = userId,
                    tableOrders = allTableOrders,
                    totalActiveTables = allTableOrders.Count,
                    dataSource = new 
                    {
                        tableOrdersCount = tableOrderResponses.Count,
                        cartBasedCount = cartBasedOrders.Count(),
                        hybridApproach = true
                    },
                    retrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // İngilizce teknik log - RKSV uyumlu
                _logger.LogError(ex, "🚨 Error retrieving table orders for recovery - UserId: {UserId}", 
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
                
                return StatusCode(500, new { 
                    success = false, 
                    message = "Internal server error during table orders recovery" 
                });
            }
        }
        // ============================================
        // QUANTITY MANAGEMENT ENDPOINTS
        // ============================================

        // Helper: Get cart item with ownership validation
        private async Task<(CartItem? Item, Cart? Cart, string? UserId, string? Error)> GetOwnedCartItemAsync(Guid itemId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return (null, null, null, "User not authenticated");

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId);

            if (cartItem == null)
                return (null, null, userId, "Cart item not found");

            if (cartItem.Cart.UserId != userId)
                return (null, null, userId, "You do not have access to this cart");

            return (cartItem, cartItem.Cart, userId, null);
        }

        // POST: api/cart/items/{itemId}/increment - Increase quantity by 1
        [HttpPost("items/{itemId}/increment")]
        public async Task<IActionResult> IncrementItemQuantity(Guid itemId)
        {
            try
            {
                var (cartItem, cart, userId, error) = await GetOwnedCartItemAsync(itemId);
                
                if (error != null)
                {
                    if (error == "User not authenticated")
                        return Unauthorized(new { message = error });
                    if (error == "You do not have access to this cart")
                        return StatusCode(403, new { message = error });
                    return NotFound(new { message = error });
                }

                cartItem!.Quantity += 1;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Item quantity incremented: ItemId={ItemId}, NewQuantity={Quantity}, UserId={UserId}",
                    itemId, cartItem.Quantity, userId);

                return Ok(new { 
                    message = "Quantity incremented successfully",
                    itemId = itemId,
                    newQuantity = cartItem.Quantity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing item quantity: ItemId={ItemId}", itemId);
                return StatusCode(500, new { message = "Failed to update quantity" });
            }
        }

        // POST: api/cart/items/{itemId}/decrement - Decrease quantity by 1 (min 1)
        [HttpPost("items/{itemId}/decrement")]
        public async Task<IActionResult> DecrementItemQuantity(Guid itemId)
        {
            try
            {
                var (cartItem, cart, userId, error) = await GetOwnedCartItemAsync(itemId);
                
                if (error != null)
                {
                    if (error == "User not authenticated")
                        return Unauthorized(new { message = error });
                    if (error == "You do not have access to this cart")
                        return StatusCode(403, new { message = error });
                    return NotFound(new { message = error });
                }

                if (cartItem!.Quantity <= 1)
                {
                    // Cannot decrement below 1 - frontend should use DELETE instead
                    return Conflict(new { 
                        message = "Cannot decrement below 1. Use DELETE to remove item.",
                        requiresRemoval = true,
                        currentQuantity = cartItem.Quantity
                    });
                }

                cartItem.Quantity -= 1;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Item quantity decremented: ItemId={ItemId}, NewQuantity={Quantity}, UserId={UserId}",
                    itemId, cartItem.Quantity, userId);

                return Ok(new { 
                    message = "Quantity decremented successfully",
                    itemId = itemId,
                    newQuantity = cartItem.Quantity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing item quantity: ItemId={ItemId}", itemId);
                return StatusCode(500, new { message = "Failed to update quantity" });
            }
        }



        // Yardımcı metod: Vergi oranını hesapla
        private decimal GetTaxRate(string taxType)
        {
            return taxType switch
            {
                "Standard" => 0.20m, // %20
                "Reduced" => 0.10m,   // %10
                "Special" => 0.13m,   // %13
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

    public class ResetCartAfterPaymentRequest
    {
        public string? Notes { get; set; }
    }

    public class ForceCleanupRequest
    {
        public int? TableNumber { get; set; } // Belirli masa için, null ise tüm sepetler
        public string Reason { get; set; } = string.Empty; // Temizlik nedeni
    }

    public class ForceCleanupResponse
    {
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<CleanedCartInfo> CleanedCarts { get; set; } = new List<CleanedCartInfo>();
        public int TotalItemsCleaned { get; set; }
        public int TotalCartsCleared { get; set; }
    }

    public class CleanedCartInfo
    {
        public string CartId { get; set; } = string.Empty;
        public int? TableNumber { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ItemsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Recovery için masa sipariş bilgi modeli
    public class TableOrderRecoveryResponse
    {
        public int? TableNumber { get; set; }
        public string CartId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<TableOrderItemInfo> Items { get; set; } = new List<TableOrderItemInfo>();
    }

    public class TableOrderItemInfo
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
    }
}
