using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Extra Zutaten: Modifier Groups ve Modifiers CRUD. Admin panel için.
    /// </summary>
    [Route("api/modifier-groups")]
    [ApiController]
    [Authorize]
    public class ModifierGroupsController : BaseController
    {
        private readonly AppDbContext _context;

        public ModifierGroupsController(AppDbContext context, ILogger<ModifierGroupsController> logger)
            : base(logger)
        {
            _context = context;
        }

        /// <summary>
        /// Tüm modifier gruplarını listele (modifier'lar + Faz 1 Products ile birlikte). Legacy: Modifiers; yeni: Products (fiyat Product'tan).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var groups = await _context.ProductModifierGroups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.SortOrder)
                    .ThenBy(g => g.Name)
                    .Include(g => g.Modifiers.Where(m => m.IsActive))
                    .Include(g => g.AddOnGroupProducts)
                    .ThenInclude(a => a.Product)
                    .ToListAsync();

                var dtos = groups.Select(g => MapToModifierGroupDto(g)).ToList();
                // Phase 2 observability: when this log stops appearing, no modifier group responses include legacy Modifiers anymore.
                var groupsWithLegacyCount = dtos.Count(g => g.Modifiers != null && g.Modifiers.Count > 0);
                if (groupsWithLegacyCount > 0)
                    _logger.LogInformation("Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers GetAll GroupsWithModifiersCount={GroupsWithModifiersCount}", groupsWithLegacyCount);
                return SuccessResponse(dtos, "Modifier groups retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.GetAll");
            }
        }

        /// <summary>
        /// Tekil modifier group getir. Legacy: Modifiers; Faz 1: Products (AddOnGroupProduct + Product; fiyat Product'tan).
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var group = await _context.ProductModifierGroups
                    .Include(g => g.Modifiers.Where(m => m.IsActive))
                    .Include(g => g.AddOnGroupProducts)
                    .ThenInclude(a => a.Product)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (group == null)
                    return ErrorResponse("Modifier group not found.", 404);

                var dto = MapToModifierGroupDto(group);
                // Phase 2 observability: when this log stops appearing, no single-group response includes legacy Modifiers anymore.
                if (dto.Modifiers != null && dto.Modifiers.Count > 0)
                    _logger.LogInformation("Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers GetById GroupId={GroupId} ModifiersCount={ModifiersCount}", id, dto.Modifiers.Count);
                return SuccessResponse(dto, "Modifier group retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.GetById");
            }
        }

        /// <summary>
        /// Yeni modifier group oluştur.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateModifierGroupRequest request)
        {
            try
            {
                var entity = new ProductModifierGroup
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    MinSelections = request.MinSelections,
                    MaxSelections = request.MaxSelections,
                    IsRequired = request.IsRequired,
                    SortOrder = request.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ProductModifierGroups.Add(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Modifier group created: {Id} {Name}", entity.Id, entity.Name);
                return SuccessResponse(new { id = entity.Id, name = entity.Name }, "Modifier group created.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Create");
            }
        }

        /// <summary>
        /// Modifier group güncelle.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateModifierGroupRequest request)
        {
            try
            {
                var entity = await _context.ProductModifierGroups.FindAsync(id);
                if (entity == null)
                    return ErrorResponse("Modifier group not found.", 404);

                entity.Name = request.Name;
                entity.MinSelections = request.MinSelections;
                entity.MaxSelections = request.MaxSelections;
                entity.IsRequired = request.IsRequired;
                entity.SortOrder = request.SortOrder;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Modifier group updated: {Id}", id);
                return SuccessResponse(new { id = entity.Id }, "Modifier group updated.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Update");
            }
        }

        /// <summary>
        /// Modifier group sil (soft: IsActive = false).
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var entity = await _context.ProductModifierGroups.FindAsync(id);
                if (entity == null)
                    return ErrorResponse("Modifier group not found.", 404);

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Modifier group deactivated: {Id}", id);
                return SuccessResponse(new { id }, "Modifier group deleted.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Delete");
            }
        }

        /// <summary>
        /// Gruba yeni modifier ekle. Legacy: creation frozen (Phase 2).
        /// Returns 410 Gone. Use POST .../products (add-on products) instead. Kept for route stability; remove in Phase 3 when no clients call it.
        /// </summary>
        [HttpPost("{groupId:guid}/modifiers")]
        [Obsolete("Legacy modifier creation is disabled. Use add-on products (POST .../products) instead.")]
        public Task<IActionResult> AddModifier(Guid groupId, [FromBody] CreateModifierRequest request)
        {
            _logger.LogWarning("Legacy modifier creation attempted for group {GroupId}; rejected (Phase 2 freeze).", groupId);
            return Task.FromResult<IActionResult>(ErrorResponse(
                "Legacy modifier creation is disabled. Use add-on products (POST /api/modifier-groups/{id}/products) instead.",
                410)); // 410 Gone = resource no longer available for creation
        }

        /// <summary>
        /// Faz 1: Gruba product ekle (mevcut product veya yeni Zusatzprodukt). Fiyat her zaman Product tablosundan; grup satırı fiyat taşımaz.
        /// </summary>
        [HttpPost("{id:guid}/products")]
        public async Task<IActionResult> AddProductToGroup(Guid id, [FromBody] AddProductToGroupRequest request)
        {
            try
            {
                if (request == null)
                    return ErrorResponse("Request body is required.", 400);

                bool hasProductId = request.ProductId.HasValue && request.ProductId.Value != Guid.Empty;
                bool hasCreateNew = request.CreateNewAddOnProduct != null;

                if (hasProductId && hasCreateNew)
                    return ErrorResponse("Provide either productId or createNewAddOnProduct, not both.", 400);
                if (!hasProductId && !hasCreateNew)
                    return ErrorResponse("Provide either productId or createNewAddOnProduct.", 400);

                var group = await _context.ProductModifierGroups.FindAsync(id);
                if (group == null)
                    return ErrorResponse("Modifier group not found.", 404);

                Guid productId;
                string productName;
                decimal price;
                int taxType;
                int sortOrder;

                if (hasProductId)
                {
                    var product = await _context.Products.FindAsync(request.ProductId!.Value);
                    if (product == null)
                        return ErrorResponse("Product not found.", 404);
                    if (!product.IsActive)
                        return ErrorResponse("Product is inactive and cannot be added to the group.", 400);

                    var exists = await _context.AddOnGroupProducts
                        .AnyAsync(a => a.ModifierGroupId == id && a.ProductId == product.Id);
                    if (exists)
                        return ErrorResponse("Product is already in this group.", 409);

                    productId = product.Id;
                    productName = product.Name;
                    price = product.Price;
                    taxType = product.TaxType;
                    sortOrder = await _context.AddOnGroupProducts
                        .Where(a => a.ModifierGroupId == id)
                        .CountAsync();
                }
                else
                {
                    var create = request.CreateNewAddOnProduct!;
                    if (!create.CategoryId.HasValue || create.CategoryId.Value == Guid.Empty)
                        return ErrorResponse("CategoryId is required when creating a new add-on product.", 400);

                    var category = await _context.Categories.FindAsync(create.CategoryId.Value);
                    if (category == null)
                        return ErrorResponse("Category not found.", 404);

                    var newProduct = new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = create.Name,
                        Price = Math.Round(create.Price, 2),
                        TaxType = create.TaxType,
                        Category = category.Name,
                        CategoryId = category.Id,
                        StockQuantity = 0,
                        MinStockLevel = 0,
                        Unit = "Stk",
                        Barcode = "ADDON-" + Guid.NewGuid().ToString("N")[..12],
                        IsActive = true,
                        IsSellableAddOn = true,
                        TaxRate = TaxTypes.GetTaxRate(create.TaxType),
                        RksvProductType = RksvProductTypes.Standard,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Products.Add(newProduct);
                    productId = newProduct.Id;
                    productName = newProduct.Name;
                    price = newProduct.Price;
                    taxType = newProduct.TaxType;
                    sortOrder = create.SortOrder;
                }

                var link = new AddOnGroupProduct
                {
                    ModifierGroupId = id,
                    ProductId = productId,
                    SortOrder = sortOrder
                };
                _context.AddOnGroupProducts.Add(link);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ProductId} added to modifier group {GroupId}.", productId, id);
                var dto = new AddOnGroupProductItemDto
                {
                    ProductId = productId,
                    ProductName = productName,
                    Price = price,
                    TaxType = taxType,
                    SortOrder = sortOrder
                };
                return StatusCode(201, new { success = true, message = "Product added to group.", data = dto, timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.AddProductToGroup");
            }
        }

        /// <summary>Legacy Modifiers + Faz 1 Products (AddOnGroupProduct → Product). Fiyat Product'tan.</summary>
        private static ModifierGroupDto MapToModifierGroupDto(ProductModifierGroup g)
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
                Modifiers = (g.Modifiers ?? new List<ProductModifier>())
                    .OrderBy(m => m.SortOrder)
                    .ThenBy(m => m.Name)
                    .Select(m => new ModifierDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Price = m.Price,
                        TaxType = m.TaxType,
                        SortOrder = m.SortOrder
                    }).ToList(),
                Products = products
            };
        }
    }
}
