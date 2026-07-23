using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;



namespace KasseAPI_Final.Controllers

{

    /// <summary>

    /// Kategori yönetimi. Tek route: api/admin/categories (legacy api/Categories kaldırıldı).

    /// </summary>

    [HasPermission(AppPermissions.CategoryView)]

    [ApiController]

    [Route("api/admin/categories")]

    public class CategoriesController : ControllerBase

    {

        private readonly AppDbContext _context;

        private readonly ILogger<CategoriesController> _logger;

        private readonly ISettingsTenantResolver _settingsTenantResolver;

        private readonly IAuditLogService _auditLogService;

        private readonly ICategoryDemoResetService _categoryDemoResetService;

        private readonly IOperationLogService _operationLogs;



        public CategoriesController(

            AppDbContext context,

            ILogger<CategoriesController> logger,

            ISettingsTenantResolver settingsTenantResolver,

            IAuditLogService auditLogService,

            ICategoryDemoResetService categoryDemoResetService,

            IOperationLogService operationLogs)

        {

            _context = context;

            _logger = logger;

            _settingsTenantResolver = settingsTenantResolver;

            _auditLogService = auditLogService;

            _categoryDemoResetService = categoryDemoResetService;

            _operationLogs = operationLogs;

        }



        private string ActorUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

        private string ActorRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";



        [HttpGet]

        public async Task<ActionResult<List<CategoryDto>>> GetCategories()

        {

            try

            {

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var categories = await QueryCategoryDtos(tenantId)

                    .OrderBy(c => c.SortOrder)

                    .ThenBy(c => c.Name)

                    .ToListAsync();



                return Ok(categories);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error getting categories");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpGet("{id}")]

        public async Task<ActionResult<CategoryDto>> GetCategory(Guid id)

        {

            try

            {

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var category = await QueryCategoryDtos(tenantId)

                    .FirstOrDefaultAsync(c => c.Id == id);



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



        [HttpPost]

        [HasPermission(AppPermissions.CategoryManage)]

        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)

        {

            try

            {

                if (!ModelState.IsValid)

                {

                    return BadRequest(ModelState);

                }



                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();



                var normalizedName = request.Name.Trim();

                var key = string.IsNullOrWhiteSpace(request.Key)

                    ? CategoryKey.FromDisplayName(normalizedName)

                    : request.Key.Trim().ToLowerInvariant();



                if (!CategoryKey.IsValid(key))

                {

                    return BadRequest(new { message = "Invalid category key format" });

                }



                var existingKey = await _context.Categories
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Key == key);

                if (existingKey != null)
                {
                    return Conflict(new
                    {
                        error = "Category key already exists",
                        message = $"Eine Kategorie mit dem Schlüssel '{key}' existiert bereits.",
                    });
                }

                var existingCategory = await _context.Categories
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name.ToLower() == normalizedName.ToLower());

                if (existingCategory != null)
                {
                    return Conflict(new
                    {
                        error = "Category already exists",
                        message = $"Eine Kategorie mit dem Namen '{normalizedName}' existiert bereits.",
                    });
                }



                var fiscalCategory = request.FiscalCategory ?? CategoryKey.InferFiscalCategory(normalizedName, request.Description);

                if (fiscalCategory == RksvProductCategory.Unspecified)

                    fiscalCategory = RksvProductCategory.Food;



                var category = new Category

                {

                    TenantId = tenantId,

                    Key = key,

                    Name = normalizedName,

                    Description = request.Description,

                    Color = request.Color,

                    Icon = request.Icon,

                    SortOrder = request.SortOrder,

                    VatRate = request.DefaultTaxRate,

                    FiscalCategory = fiscalCategory,

                    IsSystemCategory = false,

                    IsActive = true

                };



                _context.Categories.Add(category);

                await _context.SaveChangesAsync();

                var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(actorUserId))
                {
                    try
                    {
                        await _operationLogs.LogAsync(
                            tenantId,
                            actorUserId,
                            OperationTypes.CreateCategory,
                            OperationEntityTypes.Category,
                            category.Id.ToString("D"),
                            beforeState: null,
                            afterState: OperationSnapshots.FromCategory(category));
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Failed to write operation log for category create {CategoryId}", category.Id);
                    }
                }

                var dto = await QueryCategoryDtos(tenantId)

                    .FirstAsync(c => c.Id == category.Id);



                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, dto);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error creating category");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpPut("{id}")]

        [HasPermission(AppPermissions.CategoryManage)]

        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)

        {

            try

            {

                if (!ModelState.IsValid)

                {

                    return BadRequest(ModelState);

                }



                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var category = await _context.Categories

                    .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

                if (category == null || !category.IsActive)

                {

                    return NotFound(new { message = "Category not found" });

                }



                var existingCategory = await _context.Categories

                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name.ToLower() == request.Name.ToLower() &&

                                           c.Id != id && c.IsActive);



                if (existingCategory != null)

                {

                    return BadRequest(new { message = "Category name already exists" });

                }



                var oldName = category.Name;

                var auditOldValues = new

                {

                    category.Key,

                    category.Name,

                    category.Description,

                    category.Icon,

                    category.Color,

                    category.SortOrder,

                    DefaultTaxRate = category.VatRate,

                };



                category.Name = request.Name;

                category.Description = request.Description;

                category.Color = request.Color;

                category.Icon = request.Icon;

                category.SortOrder = request.SortOrder;

                category.VatRate = request.DefaultTaxRate;

                category.UpdatedAt = DateTime.UtcNow;



                if (!string.Equals(oldName, request.Name, StringComparison.Ordinal))

                {

                    await _context.Products

                        .Where(p => p.TenantId == tenantId && p.CategoryId == category.Id && p.Category == oldName)

                        .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Category, request.Name));

                }



                await _context.SaveChangesAsync();



                await TryAuditCategoryUpdateAsync(

                    category,

                    auditOldValues,

                    new

                    {

                        category.Key,

                        category.Name,

                        category.Description,

                        category.Icon,

                        category.Color,

                        category.SortOrder,

                        DefaultTaxRate = category.VatRate,

                    },

                    oldName,

                    request.Name);



                return Ok(new { message = "Category updated successfully" });

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error updating category {CategoryId}", id);

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpDelete("{id}")]

        [HasPermission(AppPermissions.CategoryManage)]

        public async Task<IActionResult> DeleteCategory(Guid id)

        {

            try

            {

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var category = await _context.Categories

                    .Include(c => c.Products)

                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && c.TenantId == tenantId);



                if (category == null)

                {

                    return NotFound(new { message = "Category not found" });

                }



                if (category.IsSystemCategory)

                {

                    category.IsActive = false;

                    category.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return Ok(new { message = "Category hidden (system category cannot be permanently deleted)" });

                }



                if (category.Products.Any(p => p.IsActive))

                {

                    return BadRequest(new

                    {

                        error = "Cannot delete category with active products. Move or delete products first.",

                    });

                }



                if (category.Products.Count > 0)

                {

                    category.IsActive = false;

                    category.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return Ok(new { message = "Category deleted successfully" });

                }



                _context.Categories.Remove(category);

                await _context.SaveChangesAsync();



                return Ok();

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error deleting category {CategoryId}", id);

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpGet("{id}/products")]

        public async Task<ActionResult<IEnumerable<AdminCategoryProductDto>>> GetCategoryProducts(Guid id)

        {

            try

            {

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var category = await _context.Categories

                    .AsNoTracking()

                    .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && c.TenantId == tenantId);



                if (category == null)

                {

                    return NotFound(new { message = "Category not found" });

                }



                var products = await _context.Products

                    .AsNoTracking()

                    .Where(p => p.TenantId == tenantId && p.IsActive && p.CategoryId == category.Id)

                    .Select(p => new AdminCategoryProductDto

                    {

                        Id = p.Id,

                        Name = p.Name,

                        Description = p.Description,

                        Price = p.Price,

                        TaxType = p.TaxType,

                        TaxRate = p.TaxRate,

                        ImageUrl = p.ImageUrl,

                        StockQuantity = p.StockQuantity,

                        MinStockLevel = p.MinStockLevel,

                        Unit = p.Unit,

                        Cost = p.Cost,

                        Barcode = p.Barcode,

                        CategoryId = category.Id,

                        CategoryName = category.Name

                    })

                    .ToListAsync();



                return Ok(products);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error getting category products {CategoryId}", id);

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpGet("search")]

        public async Task<ActionResult<List<CategoryDto>>> SearchCategories([FromQuery] string query)

        {

            try

            {

                if (string.IsNullOrWhiteSpace(query))

                {

                    return await GetCategories();

                }



                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var normalized = query.Trim().ToLowerInvariant();

                var categories = await QueryCategoryDtos(tenantId)

                    .Where(c =>

                        c.Name.ToLower().Contains(normalized) ||

                        c.Key.ToLower().Contains(normalized) ||

                        (c.Description != null && c.Description.ToLower().Contains(normalized)))

                    .OrderBy(c => c.SortOrder)

                    .ThenBy(c => c.Name)

                    .ToListAsync();



                return Ok(categories);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error searching categories");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        [HttpPost("reset-demo-names")]

        [HasPermission(AppPermissions.CategoryManage)]

        public async Task<ActionResult<CategoryDemoResetResultDto>> ResetDemoCategoryNames()

        {

            try

            {

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

                var result = await _categoryDemoResetService

                    .ResetDemoDisplayNamesAsync(tenantId)

                    .ConfigureAwait(false);



                await TryAuditCategoryDemoResetAsync(tenantId, result).ConfigureAwait(false);



                return Ok(result);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error resetting demo category names");

                return StatusCode(500, new { message = "Internal server error" });

            }

        }



        private IQueryable<CategoryDto> QueryCategoryDtos(Guid tenantId) =>

            from c in _context.Categories.IgnoreQueryFilters().AsNoTracking()

            where c.TenantId == tenantId && c.IsActive

            let productCount = _context.Products.IgnoreQueryFilters().Count(p =>

                p.TenantId == tenantId && p.IsActive && p.CategoryId == c.Id)

            select new CategoryDto

            {

                Id = c.Id,

                Key = c.Key,

                Name = c.Name,

                Description = c.Description,

                Icon = c.Icon,

                Color = c.Color,

                SortOrder = c.SortOrder,

                ProductCount = productCount,

                DefaultTaxRate = c.VatRate,

                FiscalCategory = c.FiscalCategory,

                IsSystemCategory = c.IsSystemCategory,

                OriginalDemoName = c.OriginalDemoName,

                IsActive = c.IsActive,

            };



        private async Task TryAuditCategoryUpdateAsync(

            Category category,

            object oldValues,

            object newValues,

            string oldName,

            string newName)

        {

            try

            {

                var description = string.Equals(oldName, newName, StringComparison.Ordinal)

                    ? $"Category '{category.Key}' updated."

                    : $"Category name changed from '{oldName}' to '{newName}' (key '{category.Key}' unchanged).";



                await _auditLogService.LogEntityChangeAsync(

                    AuditLogActions.CATEGORY_UPDATED,

                    AuditLogEntityTypes.CATEGORY,

                    category.Id,

                    ActorUserId,

                    ActorRole,

                    oldValues: oldValues,

                    newValues: newValues,

                    description: description);

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Audit log failed for category update CategoryId={CategoryId}", category.Id);

            }

        }



        private async Task TryAuditCategoryDemoResetAsync(Guid tenantId, CategoryDemoResetResultDto result)

        {

            try

            {

                await _auditLogService.LogSystemOperationAsync(

                    AuditLogActions.CATEGORY_DEMO_RESET,

                    AuditLogEntityTypes.CATEGORY,

                    ActorUserId,

                    ActorRole,

                    description: $"Reset {result.ResetCount} demo category display name(s) for tenant {tenantId:D}.",

                    requestData: result,

                    actionType: AuditEventType.CategoryDemoReset,

                    tenantId: tenantId);

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Audit log failed for demo category reset TenantId={TenantId}", tenantId);

            }

        }

    }



    public class CreateCategoryRequest

    {

        [Required]

        [MaxLength(100)]

        public string Name { get; set; } = string.Empty;



        [MaxLength(100)]

        public string? Key { get; set; }



        [MaxLength(500)]

        public string? Description { get; set; }



        [MaxLength(20)]

        public string? Color { get; set; }



        [MaxLength(50)]

        public string? Icon { get; set; }



        public int SortOrder { get; set; } = 0;



        [Range(0, 100)]

        public decimal DefaultTaxRate { get; set; } = 20m;



        [JsonPropertyName("vatRate")]

        public decimal VatRate

        {

            get => DefaultTaxRate;

            set => DefaultTaxRate = value;

        }



        public RksvProductCategory? FiscalCategory { get; set; }

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



        [Range(0, 100)]

        public decimal DefaultTaxRate { get; set; } = 20m;



        [JsonPropertyName("vatRate")]

        public decimal VatRate

        {

            get => DefaultTaxRate;

            set => DefaultTaxRate = value;

        }

    }

}


