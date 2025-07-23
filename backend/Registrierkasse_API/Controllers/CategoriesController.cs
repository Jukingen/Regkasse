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
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(AppDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories([FromQuery] bool includeProductCount = true)
        {
            try
            {
                var query = _context.Categories.AsQueryable();

                if (includeProductCount)
                {
                    // Ürün sayısını hesapla
                    var categoriesWithCount = await query
                        .Select(c => new CategoryDto
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Description = c.Description,
                            Color = c.Color,
                            Icon = c.Icon,
                            SortOrder = c.SortOrder,
                            IsActive = c.IsActive,
                            ProductCount = _context.Products.Count(p => p.Category == c.Name && p.IsActive),
                            CreatedAt = c.CreatedAt,
                            UpdatedAt = c.UpdatedAt
                        })
                        .OrderBy(c => c.SortOrder)
                        .ThenBy(c => c.Name)
                        .ToListAsync();

                    return Ok(categoriesWithCount);
                }
                else
                {
                    var categories = await query
                        .OrderBy(c => c.SortOrder)
                        .ThenBy(c => c.Name)
                        .ToListAsync();

                    return Ok(categories.Select(c => new CategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        Color = c.Color,
                        Icon = c.Icon,
                        SortOrder = c.SortOrder,
                        IsActive = c.IsActive,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching categories");
                return StatusCode(500, new { error = "Kategoriler yüklenirken hata oluştu" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryDto>> GetCategory(Guid id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                {
                    return NotFound();
                }

                var categoryDto = new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    Color = category.Color,
                    Icon = category.Icon,
                    SortOrder = category.SortOrder,
                    IsActive = category.IsActive,
                    ProductCount = await _context.Products.CountAsync(p => p.Category == category.Name && p.IsActive),
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt
                };

                return Ok(categoryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching category with ID: {id}");
                return StatusCode(500, new { error = "Kategori yüklenirken hata oluştu" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kategori adı benzersizlik kontrolü
                if (await _context.Categories.AnyAsync(c => c.Name == request.Name))
                {
                    return BadRequest(new { error = "Bu kategori adı zaten kullanılıyor" });
                }

                var category = new Category
                {
                    Name = request.Name,
                    Description = request.Description,
                    Color = request.Color,
                    Icon = request.Icon,
                    SortOrder = request.SortOrder,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Category created: {category.Name} by user: {User.Identity?.Name}");

                var categoryDto = new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    Color = category.Color,
                    Icon = category.Icon,
                    SortOrder = category.SortOrder,
                    IsActive = category.IsActive,
                    ProductCount = 0,
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt
                };

                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, categoryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(500, new { error = "Kategori oluşturulurken hata oluştu" });
            }
        }

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
                    return NotFound();
                }

                // Kategori adı benzersizlik kontrolü (kendisi hariç)
                if (await _context.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id))
                {
                    return BadRequest(new { error = "Bu kategori adı zaten kullanılıyor" });
                }

                category.Name = request.Name;
                category.Description = request.Description;
                category.Color = request.Color;
                category.Icon = request.Icon;
                category.SortOrder = request.SortOrder;
                category.IsActive = request.IsActive;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Category updated: {category.Name} by user: {User.Identity?.Name}");

                return Ok(new { message = "Kategori başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating category with ID: {id}");
                return StatusCode(500, new { error = "Kategori güncellenirken hata oluştu" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                {
                    return NotFound();
                }

                // Kategoriye ait ürün var mı kontrol et
                var productCount = await _context.Products.CountAsync(p => p.Category == category.Name && p.IsActive);
                if (productCount > 0)
                {
                    return BadRequest(new { error = $"Bu kategoriye ait {productCount} ürün bulunuyor. Önce ürünleri başka kategorilere taşıyın." });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Category deleted: {category.Name} by user: {User.Identity?.Name}");

                return Ok(new { message = "Kategori başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting category with ID: {id}");
                return StatusCode(500, new { error = "Kategori silinirken hata oluştu" });
            }
        }

        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateCategoryStatus(Guid id, [FromBody] UpdateCategoryStatusRequest request)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                {
                    return NotFound();
                }

                category.IsActive = request.IsActive;
                category.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Category status updated: {category.Name} - Active: {category.IsActive} by user: {User.Identity?.Name}");

                return Ok(new { message = "Kategori durumu güncellendi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating category status with ID: {id}");
                return StatusCode(500, new { error = "Kategori durumu güncellenirken hata oluştu" });
            }
        }

        [HttpGet("names")]
        public async Task<ActionResult<IEnumerable<string>>> GetCategoryNames()
        {
            try
            {
                var categoryNames = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => c.Name)
                    .ToListAsync();

                return Ok(categoryNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category names");
                return StatusCode(500, new { error = "Kategori isimleri yüklenirken hata oluştu" });
            }
        }
    }

    // DTOs
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateCategoryStatusRequest
    {
        public bool IsActive { get; set; }
    }
}  