using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Controllers.Base;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Admin: Ürün listesi (sayfalama/arama), tekil ürün, CRUD, stok, modifier-groups. Route: api/admin/products.
    /// </summary>
    [Route("api/admin/products")]
    [ApiController]
    [HasPermission(AppPermissions.ProductManage)]
    public class AdminProductsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Product> _productRepository;

        public AdminProductsController(
            AppDbContext context,
            IGenericRepository<Product> productRepository,
            ILogger<AdminProductsController> logger)
            : base(logger)
        {
            _context = context;
            _productRepository = productRepository;
        }

        /// <summary>
        /// Ürün listesi (sayfalama, opsiyonel categoryId ve name araması). GET api/admin/products
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? categoryId = null,
            [FromQuery] string? name = null)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);

                var query = _context.Products.Where(p => p.IsActive);
                if (categoryId.HasValue)
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(p => p.Name.ToLower().Contains(name.Trim().ToLower()));

                var totalCount = await query.CountAsync();

                var products = await query
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .Skip((validPageNumber - 1) * validPageSize)
                    .Take(validPageSize)
                    .ToListAsync();

                var items = products.Select(AdminProductDto.FromProduct).ToList();
                var response = new
                {
                    items,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / validPageSize)
                    }
                };

                _logger.LogInformation("Admin products list: page {Page}, total {Total}", validPageNumber, totalCount);
                return SuccessResponse(response, $"Retrieved {items.Count} products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.GetList");
            }
        }

        /// <summary>
        /// Tekil ürün. GET api/admin/products/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                    return ErrorResponse("Product not found", 404);

                return SuccessResponse(AdminProductDto.FromProduct(product), "Product retrieved");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.GetById");
            }
        }

        /// <summary>
        /// Ürün ara (name, category adı). GET api/admin/products/search
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? name = null, [FromQuery] string? category = null)
        {
            try
            {
                var query = _context.Products.Where(p => p.IsActive);
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(p => p.Name.ToLower().Contains(name.Trim().ToLower()));
                if (!string.IsNullOrWhiteSpace(category))
                    query = query.Where(p => p.Category != null && p.Category.ToLower().Contains(category.Trim().ToLower()));

                var products = await query.OrderBy(p => p.Name).ToListAsync();
                var items = products.Select(AdminProductDto.FromProduct).ToList();
                _logger.LogInformation("Admin products search: found {Count} products", items.Count);
                return SuccessResponse(items, $"Found {items.Count} products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.Search");
            }
        }

        /// <summary>
        /// Yeni ürün oluştur. POST api/admin/products
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                    return ErrorResponse("Invalid product data", 400);

                var validationResult = await ValidateProductForRKSVAsync(product);
                if (!validationResult.IsValid)
                    return ErrorResponse(validationResult.ErrorMessage!, 400);

                var category = await _context.Categories.FindAsync(product.CategoryId);
                if (category == null || !category.IsActive)
                    return ErrorResponse("Category not found or inactive", 400);
                product.Category = category.Name;

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                product.CreatedBy = User.Identity?.Name ?? "system";
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.IsActive = true;

                var createdProduct = await _productRepository.AddAsync(product);
                _logger.LogInformation("Admin product created: {Name} (ID: {Id})", product.Name, createdProduct.Id);
                return SuccessResponse(AdminProductDto.FromProduct(createdProduct), "Product created successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.Create");
            }
        }

        /// <summary>
        /// Ürün güncelle. PUT api/admin/products/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Product product)
        {
            try
            {
                if (id != product.Id)
                    return ErrorResponse("ID mismatch", 400);

                if (!ModelState.IsValid)
                    return ErrorResponse("Invalid product data", 400);

                var validationResult = await ValidateProductForRKSVAsync(product);
                if (!validationResult.IsValid)
                    return ErrorResponse(validationResult.ErrorMessage!, 400);

                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                    return ErrorResponse("Product not found", 404);

                if (product.CategoryId != Guid.Empty)
                {
                    var category = await _context.Categories.FindAsync(product.CategoryId);
                    if (category == null || !category.IsActive)
                        return ErrorResponse("Category not found or inactive", 400);
                    product.Category = category.Name;
                }

                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.CreatedAt = existingProduct.CreatedAt;
                product.CreatedBy = existingProduct.CreatedBy;

                var updatedProduct = await _productRepository.UpdateAsync(product);
                _logger.LogInformation("Admin product updated: {Name} (ID: {Id})", product.Name, id);
                return SuccessResponse(AdminProductDto.FromProduct(updatedProduct), "Product updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.Update");
            }
        }

        /// <summary>
        /// Ürün stok güncelle. PUT api/admin/products/stock/{id}
        /// </summary>
        [HttpPut("stock/{id:guid}")]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request)
        {
            try
            {
                if (request.Quantity < 0)
                    return ErrorResponse("Stock quantity cannot be negative", 400);

                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                    return ErrorResponse("Product not found", 404);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var oldStock = product.StockQuantity;
                    product.StockQuantity = request.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = User.Identity?.Name ?? "system";
                    await _productRepository.UpdateAsync(product);
                    await transaction.CommitAsync();
                    _logger.LogInformation("Admin product stock updated: {Name} (ID: {Id}) Old: {Old}, New: {New}", product.Name, id, oldStock, request.Quantity);
                    return SuccessResponse(AdminProductDto.FromProduct(product), "Product stock updated successfully");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.UpdateStock");
            }
        }

        /// <summary>
        /// Ürün sil (soft delete). DELETE api/admin/products/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var deleted = await _productRepository.DeleteAsync(id);
                if (!deleted)
                    return ErrorResponse("Product not found", 404);
                _logger.LogInformation("Admin product deleted: {Id}", id);
                return SuccessResponse(new { id }, "Product deleted successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.Delete");
            }
        }

        /// <summary>
        /// Ürüne atanmış modifier gruplarını getir. GET api/admin/products/{id}/modifier-groups
        /// </summary>
        [HttpGet("{id:guid}/modifier-groups")]
        public async Task<IActionResult> GetProductModifierGroups(Guid id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return ErrorResponse("Product not found.", 404);

                var assignmentGroupIds = await _context.ProductModifierGroupAssignments
                    .Where(a => a.ProductId == id)
                    .OrderBy(a => a.SortOrder)
                    .Select(a => a.ModifierGroupId)
                    .ToListAsync();

                if (assignmentGroupIds.Count == 0)
                    return SuccessResponse(new List<ModifierGroupDto>(), "No modifier groups assigned.");

                // Phase D PR-D: Admin product edit uses this only for assigned group IDs; return products-only (no Modifiers).
                var groups = await _context.ProductModifierGroups
                    .Where(g => g.IsActive && assignmentGroupIds.Contains(g.Id))
                    .Include(g => g.AddOnGroupProducts)
                    .ThenInclude(a => a.Product)
                    .OrderBy(g => g.SortOrder)
                    .ThenBy(g => g.Name)
                    .ToListAsync();

                var ordered = assignmentGroupIds
                    .Select(gid => groups.FirstOrDefault(g => g.Id == gid))
                    .Where(g => g != null)
                    .Cast<ProductModifierGroup>()
                    .ToList();

                var dtos = ordered.Select(g => MapToModifierGroupDtoForAdminProduct(g)).ToList();

                return SuccessResponse(dtos, "Product modifier groups retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.GetProductModifierGroups");
            }
        }

        /// <summary>
        /// Ürüne modifier gruplarını ata. POST api/admin/products/{id}/modifier-groups
        /// </summary>
        [HttpPost("{id:guid}/modifier-groups")]
        public async Task<IActionResult> SetProductModifierGroups(Guid id, [FromBody] SetProductModifierGroupsRequest request)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return ErrorResponse("Product not found.", 404);

                var existing = await _context.ProductModifierGroupAssignments
                    .Where(a => a.ProductId == id)
                    .ToListAsync();
                _context.ProductModifierGroupAssignments.RemoveRange(existing);

                if (request.ModifierGroupIds != null && request.ModifierGroupIds.Count > 0)
                {
                    var groupIds = request.ModifierGroupIds.Distinct().ToList();
                    var existingGroups = await _context.ProductModifierGroups
                        .Where(g => g.IsActive && groupIds.Contains(g.Id))
                        .Select(g => g.Id)
                        .ToListAsync();

                    int sortOrder = 0;
                    foreach (var gid in groupIds)
                    {
                        if (!existingGroups.Contains(gid)) continue;
                        _context.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
                        {
                            ProductId = id,
                            ModifierGroupId = gid,
                            SortOrder = sortOrder++
                        });
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin product {ProductId} modifier groups updated.", id);
                return SuccessResponse(new { productId = id, count = request.ModifierGroupIds?.Count ?? 0 }, "Modifier groups updated.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.SetProductModifierGroups");
            }
        }

        private async Task<ValidationResult> ValidateProductForRKSVAsync(Product product)
        {
            if (product.Price <= 0)
                return ValidationResult.Error("Price must be greater than zero");
            if (product.StockQuantity < 0)
                return ValidationResult.Error("Stock quantity cannot be negative");
            if (product.MinStockLevel < 0)
                return ValidationResult.Error("Minimum stock level cannot be negative");
            if (product.CategoryId == Guid.Empty)
                return ValidationResult.Error("CategoryId is required");
            if (!TaxTypes.All.Contains(product.TaxType))
                return ValidationResult.Error($"Tax type must be one of: {string.Join(", ", TaxTypes.All)}");
            if (!product.IsFiscalCompliant)
                return ValidationResult.Error("Product must be fiscally compliant for RKSV standards");
            if (!product.IsTaxable && string.IsNullOrWhiteSpace(product.TaxExemptionReason))
                return ValidationResult.Error("Tax exemption reason is required for non-taxable products");
            if (!RksvProductTypes.IsValidRksvType(product.RksvProductType))
                return ValidationResult.Error($"RKSV product type must be one of: {string.Join(", ", RksvProductTypes.All)}");
            var expectedTaxRate = TaxTypes.GetTaxRate(product.TaxType);
            if (product.TaxRate != expectedTaxRate)
                return ValidationResult.Error($"Tax rate mismatch: Expected {expectedTaxRate}% for {product.TaxType} tax type");
            return ValidationResult.Success();
        }

        /// <summary>Phase D PR-D: Admin product modifier-groups endpoint returns products-only; Modifiers empty (used only for assigned group IDs).</summary>
        private static ModifierGroupDto MapToModifierGroupDtoForAdminProduct(ProductModifierGroup g)
        {
            var products = (g.AddOnGroupProducts ?? new List<AddOnGroupProduct>())
                .OrderBy(a => a.SortOrder)
                .Where(a => a.Product != null && a.Product.IsActive)
                .Select(a => new AddOnGroupProductItemDto
                {
                    ProductId = a.ProductId,
                    ProductName = a.Product!.Name,
                    Price = a.Product.Price,
                    TaxType = a.Product.TaxType,
                    SortOrder = a.SortOrder
                }).ToList();

            return new ModifierGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                MinSelections = g.MinSelections,
                MaxSelections = g.MaxSelections,
                IsRequired = g.IsRequired,
                SortOrder = g.SortOrder,
                IsActive = g.IsActive,
                Products = products,
                Modifiers = new List<ModifierDto>()
            };
        }
    }
}
