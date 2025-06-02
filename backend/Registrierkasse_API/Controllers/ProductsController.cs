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
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
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

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Stok kaydı oluştur
                var inventory = new Inventory
                {
                    ProductId = product.Id,
                    CurrentStock = 0,
                    MinimumStock = 10,
                    MaximumStock = 100,
                    LastStockUpdate = DateTime.UtcNow
                };

                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Yeni ürün oluşturuldu: {product.Name}");

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

                _logger.LogInformation($"Ürün güncellendi: {product.Name}");

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

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Ürün silindi: {product.Name}");

                return Ok(new { message = "Ürün başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan ürün silinirken bir hata oluştu");
                return StatusCode(500, new { message = "Ürün silinirken bir hata oluştu", error = ex.Message });
            }
        }

        private bool ProductExists(Guid id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
} 