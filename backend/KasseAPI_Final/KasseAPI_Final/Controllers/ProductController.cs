using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(AppDbContext context, ILogger<ProductController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Product
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} products", products.Count);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Product/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(Guid id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);

                if (product == null || !product.IsActive)
                {
                    return NotFound("Ürün bulunamadı");
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Product
        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validation
                if (product.Price < 0)
                {
                    return BadRequest("Fiyat negatif olamaz");
                }

                if (product.StockQuantity < 0)
                {
                    return BadRequest("Stok miktarı negatif olamaz");
                }

                // Barcode uniqueness check
                if (!string.IsNullOrEmpty(product.Barcode))
                {
                    var existingProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Barcode == product.Barcode && p.IsActive);
                    
                    if (existingProduct != null)
                    {
                        return BadRequest("Bu barkod zaten kullanılıyor");
                    }
                }

                product.Id = Guid.NewGuid();
                product.CreatedAt = DateTime.UtcNow;
                product.IsActive = true;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product created with ID: {Id}", product.Id);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Product/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, Product product)
        {
            try
            {
                if (id != product.Id)
                {
                    return BadRequest("ID uyuşmazlığı");
                }

                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null || !existingProduct.IsActive)
                {
                    return NotFound("Ürün bulunamadı");
                }

                // Validation
                if (product.Price < 0)
                {
                    return BadRequest("Fiyat negatif olamaz");
                }

                if (product.StockQuantity < 0)
                {
                    return BadRequest("Stok miktarı negatif olamaz");
                }

                // Barcode uniqueness check (excluding current product)
                if (!string.IsNullOrEmpty(product.Barcode))
                {
                    var duplicateProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Barcode == product.Barcode && p.Id != id && p.IsActive);
                    
                    if (duplicateProduct != null)
                    {
                        return BadRequest("Bu barkod zaten kullanılıyor");
                    }
                }

                // Update properties
                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.Cost = product.Cost;
                existingProduct.TaxType = product.TaxType;
                existingProduct.TaxRate = product.TaxRate;
                existingProduct.Barcode = product.Barcode;
                existingProduct.Category = product.Category;
                existingProduct.ImageUrl = product.ImageUrl;
                existingProduct.StockQuantity = product.StockQuantity;
                existingProduct.MinStockLevel = product.MinStockLevel;
                existingProduct.Unit = product.Unit;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Product updated with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Product/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null || !product.IsActive)
                {
                    return NotFound("Ürün bulunamadı");
                }

                // Soft delete
                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Product deleted (soft) with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Product/search?query=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Arama sorgusu gerekli");
                }

                var products = await _context.Products
                    .Where(p => p.IsActive && 
                               (p.Name.Contains(query) || 
                                p.Description.Contains(query) || 
                                p.Barcode.Contains(query) ||
                                p.Category.Contains(query)))
                    .OrderBy(p => p.Name)
                    .Take(20)
                    .ToListAsync();

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
