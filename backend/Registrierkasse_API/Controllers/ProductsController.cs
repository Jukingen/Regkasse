using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;

namespace Registrierkasse_API.Controllers
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
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Ürün kodu benzersizlik kontrolü
                if (await _context.Products.AnyAsync(p => p.Barcode == request.Barcode))
                {
                    return BadRequest(new { error = "Barcode already exists" });
                }

                var product = new Product
                {
                    Name = request.Name,
                    Description = request.Description,
                    Price = request.Price,
                    StockQuantity = request.StockQuantity,
                    MinStockLevel = request.MinStockLevel,
                    Barcode = request.Barcode,
                    Category = request.Category,
                    Unit = request.Unit,
                    TaxRate = request.TaxRate,
                    TaxType = request.TaxType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Stok kaydı oluştur
                var inventory = new Inventory
                {
                    ProductId = product.Id,
                    CurrentStock = product.StockQuantity,
                    MinimumStock = product.MinStockLevel,
                    MaximumStock = request.MaxStockLevel ?? 100,
                    LastStockUpdate = DateTime.UtcNow,
                    Location = request.Location ?? "Main Warehouse",
                    Notes = "Initial stock entry",
                    IsActive = true
                };

                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"New product created: {product.Name} - Category: {product.Category}");

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, new
                {
                    id = product.Id,
                    name = product.Name,
                    description = product.Description,
                    price = product.Price,
                    stockQuantity = product.StockQuantity,
                    minStockLevel = product.MinStockLevel,
                    barcode = product.Barcode,
                    category = product.Category,
                    unit = product.Unit,
                    taxRate = product.TaxRate,
                    isActive = product.IsActive,
                    createdAt = product.CreatedAt,
                    updatedAt = product.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return StatusCode(500, new { error = "Failed to create product", details = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return NotFound(new { error = "Product not found" });
                }

                // Ürün kodu benzersizlik kontrolü (kendi ID'si hariç)
                if (await _context.Products.AnyAsync(p => p.Barcode == request.Barcode && p.Id != id))
                {
                    return BadRequest(new { error = "Barcode already exists" });
                }

                product.Name = request.Name;
                product.Description = request.Description;
                product.Price = request.Price;
                product.StockQuantity = request.StockQuantity;
                product.MinStockLevel = request.MinStockLevel;
                product.Barcode = request.Barcode;
                product.Category = request.Category;
                product.Unit = request.Unit;
                product.TaxRate = request.TaxRate;
                product.TaxType = request.TaxType;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Product updated: {product.Name} - Category: {product.Category}");

                return Ok(new
                {
                    id = product.Id,
                    name = product.Name,
                    description = product.Description,
                    price = product.Price,
                    stockQuantity = product.StockQuantity,
                    minStockLevel = product.MinStockLevel,
                    barcode = product.Barcode,
                    category = product.Category,
                    unit = product.Unit,
                    taxRate = product.TaxRate,
                    isActive = product.IsActive,
                    createdAt = product.CreatedAt,
                    updatedAt = product.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating product with ID: {id}");
                return StatusCode(500, new { error = "Failed to update product", details = ex.Message });
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
                    return NotFound(new { error = "Product not found" });
                }

                // Soft delete - sadece aktif durumunu false yap
                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Product deleted: {product.Name} - Category: {product.Category}");

                return Ok(new { message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting product with ID: {id}");
                return StatusCode(500, new { error = "Failed to delete product", details = ex.Message });
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
                return BadRequest(new { error = "Search term is required" });
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

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateProductStatus(Guid id, [FromBody] UpdateProductStatusRequest request)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return NotFound(new { error = "Product not found" });
                }

                product.IsActive = request.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = product.Id,
                    isActive = product.IsActive,
                    updatedAt = product.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating product status with ID: {id}");
                return StatusCode(500, new { error = "Failed to update product status", details = ex.Message });
            }
        }

        private bool ProductExists(Guid id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }

    // Request Models
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public TaxType TaxType { get; set; } = TaxType.Standard;
        public int? MaxStockLevel { get; set; }
        public string? Location { get; set; }
    }

    public class UpdateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public TaxType TaxType { get; set; } = TaxType.Standard;
    }

    public class UpdateProductStatusRequest
    {
        public bool IsActive { get; set; }
    }
} 
