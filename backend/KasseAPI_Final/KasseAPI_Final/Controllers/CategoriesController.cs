using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(AppDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    // .Include(c => c.Products) // Geçici olarak kapatıldı - navigation property yok
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(Guid id)
        {
            try
            {
                var category = await _context.Categories
                    // .Include(c => c.Products) // Geçici olarak kapatıldı - navigation property yok
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                if (category == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category {CategoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/categories
        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<Category>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kategori adı benzersiz olmalı
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.IsActive);

                if (existingCategory != null)
                {
                    return BadRequest(new { message = "Category name already exists" });
                }

                var category = new Category
                {
                    Name = request.Name,
                    Description = request.Description,
                    Color = request.Color,
                    Icon = request.Icon,
                    SortOrder = request.SortOrder,
                    IsActive = true
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/categories/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null || !category.IsActive)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Kategori adı benzersiz olmalı (kendisi hariç)
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Name.ToLower() && 
                                           c.Id != id && c.IsActive);

                if (existingCategory != null)
                {
                    return BadRequest(new { message = "Category name already exists" });
                }

                category.Name = request.Name;
                category.Description = request.Description;
                category.Color = request.Color;
                category.Icon = request.Icon;
                category.SortOrder = request.SortOrder;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Category updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/categories/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            try
            {
                var category = await _context.Categories
                    // .Include(c => c.Products) // Geçici olarak kapatıldı - navigation property yok
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                if (category == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Kategoriye bağlı ürünler varsa silme - manuel kontrol
                var hasProducts = await _context.Products.AnyAsync(p => p.Category == category.Name && p.IsActive);
                if (hasProducts)
                {
                    return BadRequest(new { message = "Cannot delete category with associated products" });
                }

                // Soft delete
                category.IsActive = false;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Category deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category {CategoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/categories/{id}/products
        [HttpGet("{id}/products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetCategoryProducts(Guid id)
        {
            try
            {
                var category = await _context.Categories
                    // .Include(c => c.Products.Where(p => p.IsActive)) // Geçici olarak kapatıldı
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                if (category == null)
                {
                    return NotFound(new { message = "Category not found" });
                }

                // Manuel olarak kategoriye ait ürünleri getir
                var products = await _context.Products
                    .Where(p => p.Category == category.Name && p.IsActive)
                    .ToListAsync();

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category products {CategoryId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/categories/search?query=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Category>>> SearchCategories([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return await GetCategories();
                }

                var categories = await _context.Categories
                    .Where(c => c.IsActive && 
                               (c.Name.ToLower().Contains(query.ToLower()) || 
                                c.Description.ToLower().Contains(query.ToLower())))
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching categories");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class CreateCategoryRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? Icon { get; set; }

        public int SortOrder { get; set; } = 0;
    }

    public class UpdateCategoryRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? Icon { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
