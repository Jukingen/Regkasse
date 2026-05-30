using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Admin: Ürün listesi (sayfalama/arama), tekil ürün, CRUD, stok, modifier-groups. Route: api/admin/products.
    /// Read endpoints require ProductView (aligned with FE-Admin route guard and CategoriesController pattern); mutations require ProductManage.
    /// </summary>
    [Route("api/admin/products")]
    [ApiController]
    [HasPermission(AppPermissions.ProductView)]
    public partial class AdminProductsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IOptions<ProductMediaOptions> _productMediaOptions;
        private readonly ProductImageThumbnailService _productImageThumbnailService;
        private readonly IDemoProductImportService _demoProductImportService;
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly IAdminProductListService _productListService;

        public AdminProductsController(
            AppDbContext context,
            IGenericRepository<Product> productRepository,
            ILogger<AdminProductsController> logger,
            ISettingsTenantResolver settingsTenantResolver,
            IWebHostEnvironment hostEnvironment,
            IOptions<ProductMediaOptions> productMediaOptions,
            ProductImageThumbnailService productImageThumbnailService,
            IDemoProductImportService demoProductImportService,
            ICurrentTenantAccessor tenantAccessor,
            IAdminProductListService productListService)
            : base(logger)
        {
            _context = context;
            _productRepository = productRepository;
            _settingsTenantResolver = settingsTenantResolver;
            _hostEnvironment = hostEnvironment;
            _productMediaOptions = productMediaOptions;
            _productImageThumbnailService = productImageThumbnailService;
            _demoProductImportService = demoProductImportService;
            _tenantAccessor = tenantAccessor;
            _productListService = productListService;
        }

        /// <summary>Demo menu catalog for import selection UI. GET api/admin/products/demo/catalog</summary>
        [HttpGet("demo/catalog")]
        [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
        [ProducesResponseType(typeof(DemoImportCatalogDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<DemoImportCatalogDto>> GetDemoImportCatalog(
            [FromServices] IDemoProductImportService importService,
            CancellationToken cancellationToken = default)
        {
            var catalog = await importService.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            return Ok(catalog);
        }

        /// <summary>
        /// Import the demo menu catalog for the current tenant. POST api/admin/products/demo/import
        /// </summary>
        [HttpPost("demo/import")]
        [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
        [HasPermission(AppPermissions.ProductManage)]
        [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImportResult>> ImportDemoProducts(
            [FromBody] DemoImportRequest request,
            CancellationToken cancellationToken = default)
        {
            var tenantId = _tenantAccessor.TenantId;
            if (tenantId == null)
                return BadRequest(new { error = "No tenant context" });

            var result = await _demoProductImportService
                .ImportDemoProductsAsync(tenantId.Value, request, progress: null, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
                return StatusCode(StatusCodes.Status500InternalServerError, result);

            return Ok(result);
        }

        /// <summary>
        /// Ürün listesi (gelişmiş filtreleme, sayfalama). GET api/admin/products
        /// </summary>
        /// <param name="isActive">Legacy optional: omit or "true" = active only (default); "false" = inactive only; "all" = both.</param>
        [HttpGet]
        [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] ProductFilterDto filter,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] Guid? categoryId = null,
            [FromQuery] string? name = null,
            [FromQuery] string? isActive = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var defaultActiveOnly = MergeLegacyListParams(filter, pageNumber, pageSize, categoryId, name, isActive);
                if (defaultActiveOnly == null)
                    return BadRequest(new { message = "Invalid isActive filter. Use true, false, or all.", code = "ADMIN_PRODUCTS_INVALID_IS_ACTIVE" });

                var (response, errorCode, errorMessage) =
                    await _productListService.QueryAsync(filter, defaultActiveOnly.Value, cancellationToken);

                if (errorCode != null)
                    return BadRequest(new { message = errorMessage, code = errorCode });

                _logger.LogInformation(
                    "Admin products list: page {Page}, total {Total}, filters {FilterCount}",
                    response.Page,
                    response.TotalCount,
                    response.ActiveFilters.ActiveFilterCount);

                // SuccessResponse wrapper keeps FE axios unwrap contract; body mirrors ProductListResponse + pagination alias.
                var payload = new
                {
                    items = response.Items.Select(ProductListDtoMapper.ToAdminProductDto).ToList(),
                    pagination = new
                    {
                        pageNumber = response.Page,
                        pageSize = response.PageSize,
                        totalCount = response.TotalCount,
                        totalPages = (int)Math.Ceiling((double)response.TotalCount / response.PageSize),
                    },
                    availableFilters = response.AvailableFilters,
                    activeFilters = response.ActiveFilters,
                };

                return SuccessResponse(payload, $"Retrieved {response.Items.Count} products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.GetProducts");
            }
        }

        /// <returns><c>true</c> default active-only; <c>false</c> include inactive; <c>null</c> invalid legacy isActive.</returns>
        private static bool? MergeLegacyListParams(
            ProductFilterDto filter,
            int? pageNumber,
            int? pageSize,
            Guid? categoryId,
            string? name,
            string? isActive)
        {
            if (pageNumber.HasValue && pageNumber.Value > 0)
                filter.Page = pageNumber.Value;
            if (pageSize.HasValue && pageSize.Value > 0)
                filter.PageSize = pageSize.Value;

            if (categoryId.HasValue && categoryId.Value != Guid.Empty && filter.CategoryIds.Count == 0)
                filter.CategoryIds.Add(categoryId.Value);

            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                filter.SearchTerm = name.Trim();
                filter.SearchInName = true;
            }

            if (!string.IsNullOrWhiteSpace(isActive))
            {
                if (!AdminProductListIsActiveFilterParser.TryParse(isActive, out var activeMode, out _))
                    return null;

                filter.IsActive = activeMode switch
                {
                    AdminProductListIsActiveFilterMode.ActiveOnly => true,
                    AdminProductListIsActiveFilterMode.InactiveOnly => false,
                    _ => null,
                };
                return activeMode == AdminProductListIsActiveFilterMode.ActiveOnly;
            }

            if (filter.IsActive.HasValue)
                return filter.IsActive.Value;

            return true;
        }

        /// <summary>
        /// Tekil ürün. GET api/admin/products/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var product = await GetAdminProductByIdAsync(id);
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
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var query = _context.Products.Where(p => p.IsActive && p.TenantId == tenantId);
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
        [HasPermission(AppPermissions.ProductManage)]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                    return ErrorResponse("Invalid product data", 400);

                var validationResult = await ValidateProductForRKSVAsync(product);
                if (!validationResult.IsValid)
                    return ErrorResponse(validationResult.ErrorMessage!, 400);

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.TenantId == tenantId);
                if (category == null || !category.IsActive)
                    return ErrorResponse("Category not found or inactive", 400);
                product.Category = category.Name;

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                product.CreatedBy = User.Identity?.Name ?? "system";
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.IsActive = true;
                ProductLocalization.SyncCanonicalFields(product);

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
        [HasPermission(AppPermissions.ProductManage)]
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

                var existingProduct = await GetAdminProductByIdAsync(id);
                if (existingProduct == null)
                    return ErrorResponse("Product not found", 404);

                if (product.CategoryId != Guid.Empty)
                {
                    var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                    var category = await _context.Categories
                        .FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.TenantId == tenantId);
                    if (category == null || !category.IsActive)
                        return ErrorResponse("Category not found or inactive", 400);
                    product.Category = category.Name;
                }

                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.CreatedAt = existingProduct.CreatedAt;
                product.CreatedBy = existingProduct.CreatedBy;
                product.TenantId = existingProduct.TenantId;
                ProductLocalization.SyncCanonicalFields(product);

                _context.Entry(existingProduct).State = EntityState.Detached;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin product updated: {Name} (ID: {Id})", product.Name, id);
                return SuccessResponse(AdminProductDto.FromProduct(product), "Product updated successfully");
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
        [HasPermission(AppPermissions.ProductManage)]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request)
        {
            try
            {
                if (request.Quantity < 0)
                    return ErrorResponse("Stock quantity cannot be negative", 400);

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
                if (product == null)
                    return ErrorResponse("Product not found", 404);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var oldStock = product.StockQuantity;
                    product.StockQuantity = request.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = User.Identity?.Name ?? "system";
                    await _context.SaveChangesAsync();
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
        [HasPermission(AppPermissions.ProductManage)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var entity = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
                if (entity == null)
                    return ErrorResponse("Product not found", 404);
                if (!entity.IsActive)
                {
                    _logger.LogInformation("Admin product delete skipped (already inactive): {Id}", id);
                    return SuccessResponse(new { id }, "Product deleted successfully");
                }

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = User.Identity?.Name ?? "system";
                await _context.SaveChangesAsync();
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
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
                if (product == null)
                    return ErrorResponse("Product not found.", 404);

                var assignmentGroupIds = await _context.ProductModifierGroupAssignments
                    .Where(a => a.ProductId == id && a.TenantId == tenantId)
                    .OrderBy(a => a.SortOrder)
                    .Select(a => a.ModifierGroupId)
                    .ToListAsync();

                if (assignmentGroupIds.Count == 0)
                    return SuccessResponse(new List<ModifierGroupDto>(), "No modifier groups assigned.");

                // Phase D PR-D: Admin product edit uses this only for assigned group IDs; return products-only (no Modifiers).
                var groups = await _context.ProductModifierGroups
                    .Where(g => g.IsActive && g.TenantId == tenantId && assignmentGroupIds.Contains(g.Id))
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
        [HasPermission(AppPermissions.ProductManage)]
        public async Task<IActionResult> SetProductModifierGroups(Guid id, [FromBody] SetProductModifierGroupsRequest request)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
                if (product == null)
                    return ErrorResponse("Product not found.", 404);

                var existing = await _context.ProductModifierGroupAssignments
                    .Where(a => a.ProductId == id && a.TenantId == tenantId)
                    .ToListAsync();
                _context.ProductModifierGroupAssignments.RemoveRange(existing);

                if (request.ModifierGroupIds != null && request.ModifierGroupIds.Count > 0)
                {
                    var groupIds = request.ModifierGroupIds.Distinct().ToList();
                    var existingGroups = await _context.ProductModifierGroups
                        .Where(g => g.IsActive && g.TenantId == tenantId && groupIds.Contains(g.Id))
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
                            TenantId = tenantId,
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

        /// <summary>
        /// Upload product image (JPG, PNG, WebP; max 2 MB). Decodes input, center-crops to a square thumbnail (default 120×120), encodes WebP, stores only that file; response returns its public URL for <see cref="Product.ImageUrl"/>.
        /// POST api/admin/products/{id}/image
        /// </summary>
        [HttpPost("{id:guid}/image")]
        [HasPermission(AppPermissions.ProductManage)]
        [RequestSizeLimit(3 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProductImage(Guid id, IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return ErrorResponse("No file uploaded", 400);

                var opts = _productMediaOptions.Value;
                if (file.Length > opts.MaxBytes)
                    return ErrorResponse($"File too large (max {opts.MaxBytes} bytes)", 400);

                await using var readStream = file.OpenReadStream();
                await using var ms = new MemoryStream();
                await readStream.CopyToAsync(ms, cancellationToken);
                var bytes = ms.ToArray();
                if (bytes.Length == 0)
                    return ErrorResponse("No file uploaded", 400);
                if (bytes.Length > opts.MaxBytes)
                    return ErrorResponse($"File too large (max {opts.MaxBytes} bytes)", 400);

                var header = bytes.AsSpan(0, Math.Min(bytes.Length, 16));
                if (!ProductImageFormatDetector.TryDetect(header, out _, out _))
                    return ErrorResponse("Unsupported image type (allowed: JPG, PNG, WebP)", 400);

                byte[] thumbnailBytes;
                try
                {
                    thumbnailBytes = await _productImageThumbnailService.CreateSquareThumbnailWebpAsync(bytes, cancellationToken);
                }
                catch (ProductImageProcessingException ex)
                {
                    return ErrorResponse(ex.Message, 400);
                }

                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
                if (product == null)
                    return ErrorResponse("Product not found", 404);

                var root = Path.Combine(_hostEnvironment.ContentRootPath, opts.RootRelativeDirectory);
                var productDir = Path.Combine(root, tenantId.ToString("D"), id.ToString("D"));
                Directory.CreateDirectory(productDir);
                foreach (var existing in Directory.GetFiles(productDir))
                {
                    try
                    {
                        System.IO.File.Delete(existing);
                    }
                    catch
                    {
                        // best-effort cleanup before replacing image
                    }
                }

                var fileName = $"{Guid.NewGuid():N}.webp";
                var absolutePath = Path.Combine(productDir, fileName);
                await System.IO.File.WriteAllBytesAsync(absolutePath, thumbnailBytes, cancellationToken);

                var prefix = opts.PublicUrlPathPrefix.TrimEnd('/');
                if (!prefix.StartsWith('/'))
                    prefix = "/" + prefix;
                var baseUrl = string.IsNullOrWhiteSpace(opts.PublicBaseUrl)
                    ? $"{Request.Scheme}://{Request.Host.Value}"
                    : opts.PublicBaseUrl.Trim().TrimEnd('/');
                var publicUrl = $"{baseUrl}{prefix}/{tenantId:D}/{id:D}/{fileName}";

                _logger.LogInformation("Admin product image uploaded: product {ProductId}, tenant {TenantId}", id, tenantId);
                return SuccessResponse(new { imageUrl = publicUrl }, "Image uploaded");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminProducts.UploadProductImage");
            }
        }

        private async Task<Product?> GetAdminProductByIdAsync(Guid id)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            return await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
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
                .Where(a => a.Product != null && a.Product.TenantId == g.TenantId && a.Product.IsActive)
                .Select(a => new AddOnGroupProductItemDto
                {
                    ProductId = a.ProductId,
                    ProductName = ProductLocalization.ResolveName(a.Product!, "de"),
                    NameDe = a.Product!.NameDe,
                    NameEn = a.Product.NameEn,
                    NameTr = a.Product.NameTr,
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
