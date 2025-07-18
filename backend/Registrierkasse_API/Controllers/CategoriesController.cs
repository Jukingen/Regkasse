using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Data;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        description = c.Description,
                        color = c.Color,
                        icon = c.Icon,
                        sortOrder = c.SortOrder,
                        isActive = c.IsActive,
                        createdAt = c.CreatedAt,
                        updatedAt = c.UpdatedAt,
                        productCount = c.Products.Count
                    })
                    .OrderBy(c => c.sortOrder)
                    .ThenBy(c => c.name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve categories", details = ex.Message });
            }
        }

        // GET: api/categories/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(Guid id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { error = "Category not found" });
                }

                var result = new
                {
                    id = category.Id,
                    name = category.Name,
                    description = category.Description,
                    color = category.Color,
                    icon = category.Icon,
                    sortOrder = category.SortOrder,
                    isActive = category.IsActive,
                    createdAt = category.CreatedAt,
                    updatedAt = category.UpdatedAt,
                    productCount = category.Products?.Count ?? 0,
                    products = category.Products?.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        stockQuantity = p.StockQuantity,
                        isActive = p.IsActive
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve category", details = ex.Message });
            }
        }

        // POST: api/categories
        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // İsim benzersizlik kontrolü
                if (await _context.Categories.AnyAsync(c => c.Name == request.Name))
                {
                    return BadRequest(new { error = "Category name already exists" });
                }

                var category = new Category
                {
                    Name = request.Name,
                    Description = request.Description,
                    Color = request.Color,
                    Icon = request.Icon,
                    SortOrder = request.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new
                {
                    id = category.Id,
                    name = category.Name,
                    description = category.Description,
                    color = category.Color,
                    icon = category.Icon,
                    sortOrder = category.SortOrder,
                    isActive = category.IsActive,
                    createdAt = category.CreatedAt,
                    updatedAt = category.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create category", details = ex.Message });
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
                if (category == null)
                {
                    return NotFound(new { error = "Category not found" });
                }

                // İsim benzersizlik kontrolü (kendisi hariç)
                if (await _context.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id))
                {
                    return BadRequest(new { error = "Category name already exists" });
                }

                category.Name = request.Name;
                category.Description = request.Description;
                category.Color = request.Color;
                category.Icon = request.Icon;
                category.SortOrder = request.SortOrder;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = category.Id,
                    name = category.Name,
                    description = category.Description,
                    color = category.Color,
                    icon = category.Icon,
                    sortOrder = category.SortOrder,
                    isActive = category.IsActive,
                    createdAt = category.CreatedAt,
                    updatedAt = category.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update category", details = ex.Message });
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
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { error = "Category not found" });
                }

                // Kategoride ürün varsa silme
                if (category.Products?.Any() == true)
                {
                    return BadRequest(new { error = "Cannot delete category with existing products" });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Category deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete category", details = ex.Message });
            }
        }

        // PATCH: api/categories/{id}/status
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateCategoryStatus(Guid id, [FromBody] UpdateCategoryStatusRequest request)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { error = "Category not found" });
                }

                category.IsActive = request.IsActive;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = category.Id,
                    isActive = category.IsActive,
                    updatedAt = category.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update category status", details = ex.Message });
            }
        }

        // GET: api/categories/{id}/products
        [HttpGet("{id}/products")]
        public async Task<IActionResult> GetCategoryProducts(Guid id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products.Where(p => p.IsActive))
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { error = "Category not found" });
                }

                var products = category.Products.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    description = p.Description,
                    price = p.Price,
                    stockQuantity = p.StockQuantity,
                    minStockLevel = p.MinStockLevel,
                    barcode = p.Barcode,
                    unit = p.Unit,
                    isActive = p.IsActive
                }).ToList();

                return Ok(new
                {
                    category = new
                    {
                        id = category.Id,
                        name = category.Name,
                        description = category.Description
                    },
                    products = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve category products", details = ex.Message });
            }
        }
    }

    // Request Models
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpdateCategoryStatusRequest
    {
        public bool IsActive { get; set; }
    }
} 