using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data.Repositories;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Ürün yönetimi için controller
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : EntityController<Product>
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Product> _productRepository;

        public ProductController(
            AppDbContext context, 
            IGenericRepository<Product> productRepository,
            ILogger<ProductController> logger) : base(productRepository, logger)
        {
            _context = context;
            _productRepository = productRepository;
        }

        /// <summary>
        /// Barkod'a göre ürün getir
        /// </summary>
        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetByBarcode(string barcode)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Barcode == barcode && p.IsActive);

                if (product == null)
                {
                    return ErrorResponse($"Product with barcode {barcode} not found", 404);
                }

                return SuccessResponse(product, "Product retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByBarcode with barcode {barcode}");
            }
        }

        /// <summary>
        /// Kategoriye göre ürünleri getir
        /// </summary>
        [HttpGet("category/{categoryName}")]
        public async Task<IActionResult> GetByCategory(string categoryName)
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.Category == categoryName && p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                return SuccessResponse(products, $"Retrieved {products.Count} products in category {categoryName}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByCategory with category name {categoryName}");
            }
        }

        /// <summary>
        /// Stok durumuna göre ürünleri getir
        /// </summary>
        [HttpGet("stock/{status}")]
        public async Task<IActionResult> GetByStockStatus(string status)
        {
            try
            {
                var products = status.ToLower() switch
                {
                    "in-stock" => await _context.Products
                        .Where(p => p.StockQuantity > 0 && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    "out-of-stock" => await _context.Products
                        .Where(p => p.StockQuantity <= 0 && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    "low-stock" => await _context.Products
                        .Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.MinStockLevel && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    _ => new List<Product>()
                };

                return SuccessResponse(products, $"Retrieved {products.Count} products with stock status: {status}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByStockStatus with status {status}");
            }
        }

        /// <summary>
        /// Ürün oluştur (özel validation ile)
        /// </summary>
        [HttpPost]
        public override async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                // Özel validation
                var validationErrors = await ValidateProductAsync(product);
                if (validationErrors.Any())
                {
                    return ErrorResponse("Validation failed", 400, validationErrors);
                }

                var createdProduct = await _productRepository.AddAsync(product);
                
                return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, 
                    SuccessResponse(createdProduct, "Product created successfully"));
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create Product");
            }
        }

        /// <summary>
        /// Ürün güncelle (özel validation ile)
        /// </summary>
        [HttpPut("{id}")]
        public override async Task<IActionResult> Update(Guid id, [FromBody] Product product)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                if (id != product.Id)
                {
                    return ErrorResponse("ID mismatch between URL and request body", 400);
                }

                // Özel validation
                var validationErrors = await ValidateProductAsync(product, id);
                if (validationErrors.Any())
                {
                    return ErrorResponse("Validation failed", 400, validationErrors);
                }

                var updatedProduct = await _productRepository.UpdateAsync(product);
                
                return SuccessResponse(updatedProduct, "Product updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Update Product with ID {id}");
            }
        }

        /// <summary>
        /// Ürün validation
        /// </summary>
        private async Task<List<string>> ValidateProductAsync(Product product, Guid? excludeId = null)
        {
            var errors = new List<string>();

            // Fiyat kontrolü
            if (product.Price < 0)
            {
                errors.Add("Price cannot be negative");
            }

            // Stok miktarı kontrolü
            if (product.StockQuantity < 0)
            {
                errors.Add("Stock quantity cannot be negative");
            }

            // Barkod benzersizlik kontrolü
            if (!string.IsNullOrEmpty(product.Barcode))
            {
                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Barcode == product.Barcode && 
                                            p.IsActive && 
                                            p.Id != excludeId);
                
                if (existingProduct != null)
                {
                    errors.Add("This barcode is already in use");
                }
            }

            // Ürün adı kontrolü
            if (string.IsNullOrWhiteSpace(product.Name))
            {
                errors.Add("Product name is required");
            }

            return errors;
        }

        /// <summary>
        /// Ürün arama
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? name, [FromQuery] string? barcode, [FromQuery] string? category)
        {
            try
            {
                var query = _context.Products.Where(p => p.IsActive);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    query = query.Where(p => p.Name.Contains(name));
                }

                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    query = query.Where(p => p.Barcode != null && p.Barcode.Contains(barcode));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(p => p.Category.Contains(category));
                }

                var products = await query
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                return SuccessResponse(products, $"Found {products.Count} products matching search criteria");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Search Products");
            }
        }

        /// <summary>
        /// Stok güncelle
        /// </summary>
        [HttpPut("{id}/stock")]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return ErrorResponse($"Product with ID {id} not found", 404);
                }

                if (request.Quantity < 0)
                {
                    return ErrorResponse("Stock quantity cannot be negative", 400);
                }

                product.StockQuantity = request.Quantity;
                product.UpdatedAt = DateTime.UtcNow;

                var updatedProduct = await _productRepository.UpdateAsync(product);
                
                return SuccessResponse(updatedProduct, "Stock updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"UpdateStock for Product with ID {id}");
            }
        }
    }

    /// <summary>
    /// Stok güncelleme request modeli
    /// </summary>
    public class UpdateStockRequest
    {
        public int Quantity { get; set; }
    }
}
