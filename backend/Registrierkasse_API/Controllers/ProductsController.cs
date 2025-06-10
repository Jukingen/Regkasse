using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext context, ILogger<ProductsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] string? category = null, [FromQuery] string? search = null)
        {
            var query = _context.Products.Where(p => p.IsActive);

            // Kategori filtresi
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Arama filtresi
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            return await query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategories()
        {
            var categories = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(Guid id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> CreateProduct([FromBody] Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Ürün kodu benzersizlik kontrolü
                if (await _context.Products.AnyAsync(p => p.Barcode == product.Barcode))
                {
                    return BadRequest(new { message = "Bu barkod zaten kullanılıyor" });
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Stok kaydı oluştur
                var inventory = new Inventory
                {
                    ProductId = product.Id,
                    CurrentStock = product.StockQuantity,
                    MinimumStock = product.MinStockLevel,
                    MaximumStock = 100,
                    LastStockUpdate = DateTime.UtcNow
                };

                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Yeni ürün oluşturuldu: {product.Name} - Kategori: {product.Category}");

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, 
                    new { message = "Ürün başarıyla oluşturuldu", product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ürün oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = "Ürün oluşturulurken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] Product product)
        {
            try
            {
                if (id != product.Id)
                {
                    return BadRequest(new { message = "ID'ler eşleşmiyor" });
                }

                // Ürün kodu benzersizlik kontrolü (kendi ID'si hariç)
                if (await _context.Products.AnyAsync(p => p.Barcode == product.Barcode && p.Id != id))
                {
                    return BadRequest(new { message = "Bu barkod zaten kullanılıyor" });
                }

                _context.Entry(product).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(id))
                    {
                        return NotFound(new { message = $"ID: {id} olan ürün bulunamadı" });
                    }
                    throw;
                }

                _logger.LogInformation($"Ürün güncellendi: {product.Name} - Kategori: {product.Category}");

                return Ok(new { message = "Ürün başarıyla güncellendi", product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan ürün güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Ürün güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return NotFound(new { message = $"ID: {id} olan ürün bulunamadı" });
                }

                // Soft delete - sadece aktif durumunu false yap
                product.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Ürün silindi: {product.Name} - Kategori: {product.Category}");

                return Ok(new { message = "Ürün başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan ürün silinirken bir hata oluştu");
                return StatusCode(500, new { message = "Ürün silinirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("by-category/{category}")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProductsByCategory(string category)
        {
            var products = await _context.Products
                .Where(p => p.IsActive && p.Category == category)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest(new { message = "Arama terimi gerekli" });
            }

            var products = await _context.Products
                .Where(p => p.IsActive && 
                           (p.Name.Contains(q) || 
                            p.Description.Contains(q) || 
                            p.Category.Contains(q) ||
                            p.Barcode.Contains(q)))
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return Ok(products);
        }

        private bool ProductExists(Guid id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
} 