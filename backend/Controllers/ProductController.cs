using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Ürün yönetimi için controller - RKSV uyumlu ürün işlemleri
    /// </summary>
    [Route("api/Product")]
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
    /// Tüm aktif ürünleri getir (sayfalama ile)
    /// </summary>
    [HttpGet]
    public override async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var query = _context.Products.Where(p => p.IsActive);
                var totalCount = await query.CountAsync();
                
                var products = await query
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .Skip((validPageNumber - 1) * validPageSize)
                    .Take(validPageSize)
                    .ToListAsync();
                
                var response = new
                {
                    items = products,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / validPageSize)
                    }
                };

                // İngilizce teknik log
                _logger.LogInformation($"Retrieved {products.Count} active products from page {validPageNumber}");
                
                return SuccessResponse(response, $"Retrieved {products.Count} active products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetAll");
            }
        }

        /// <summary>
        /// Tüm aktif ürünleri tek çağrıda getir (sayfalama yok)
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllWithoutPagination()
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {products.Count} active products without pagination");
                return SuccessResponse(products, $"Retrieved {products.Count} active products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetAllWithoutPagination");
            }
        }

        /// <summary>
        /// Katalog: Kategorileri sabit ID'lerle ve ürünleri categoryId ile birlikte döndürür
        /// </summary>
        [HttpGet("catalog")]
        public async Task<IActionResult> GetCatalog()
        {
            try
            {
                var activeProducts = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                // Kategori isimlerinden deterministik GUID üret (case-insensitive)
                Guid GenerateCategoryId(string categoryName)
                {
                    if (string.IsNullOrWhiteSpace(categoryName))
                    {
                        categoryName = "Uncategorized";
                    }
                    var normalized = categoryName.Trim().ToLowerInvariant();
                    // Stabil olması için Name-based GUID (V5 benzeri) - .NET'te hazır yok, basit hash ile seedle
                    var bytes = Encoding.UTF8.GetBytes(normalized);
                    var hash = SHA1.HashData(bytes);
                    var guidBytes = new byte[16];
                    Array.Copy(hash, guidBytes, 16);
                    return new Guid(guidBytes);
                }

                var categories = activeProducts
                    .Select(p => p.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .Select(name => new
                    {
                        Id = GenerateCategoryId(name),
                        Name = name
                    })
                    .ToList();

                // Ürünleri categoryId ile eşleştir - duplicate property isimlerini önle
                var products = activeProducts.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.ImageUrl,
                    p.StockQuantity,
                    p.MinStockLevel,
                    p.Unit,
                    ProductCategory = p.Category, // Duplicate property ismini önle
                    CategoryId = GenerateCategoryId(p.Category ?? "Uncategorized"),
                    p.TaxType,
                    p.TaxRate,
                    p.IsActive,
                    p.IsFiscalCompliant,
                    p.FiscalCategoryCode,
                    p.IsTaxable,
                    p.TaxExemptionReason,
                    p.RksvProductType,
                    p.Cost
                }).ToList();

                var response = new
                {
                    Categories = categories,
                    Products = products
                };

                _logger.LogInformation($"Catalog built: {categories.Count} categories, {products.Count} products");
                return SuccessResponse(response, $"Retrieved catalog with {categories.Count} categories and {products.Count} products");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetCatalog");
            }
        }

        /// <summary>
        /// Ana sayfa için tüm aktif ürünleri getir (kategori bazlı gruplandırılmış)
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetAllActiveProducts()
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                // Kategori bazlı gruplandırma
                var groupedProducts = products
                    .GroupBy(p => p.Category)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Products = g.Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Description,
                            p.Price,
                            p.ImageUrl,
                            p.StockQuantity,
                            p.TaxType,
                            p.TaxRate,
                            p.IsActive
                        }).ToList()
                    })
                    .OrderBy(g => g.Category)
                    .ToList();

                // İngilizce teknik log
                _logger.LogInformation($"Retrieved {products.Count} active products grouped by {groupedProducts.Count} categories");
                
                return SuccessResponse(groupedProducts, $"Retrieved {products.Count} active products grouped by {groupedProducts.Count} categories");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetAllActiveProducts");
            }
        }

        /// <summary>
        /// Debug: Veritabanındaki mevcut kategorileri ve ürünleri göster
        /// </summary>
        [HttpGet("debug/categories-products")]
        public async Task<IActionResult> GetDebugCategoriesAndProducts()
        {
            try
            {
                var allProducts = await _context.Products.ToListAsync();
                var activeProducts = allProducts.Where(p => p.IsActive).ToList();
                var inactiveProducts = allProducts.Where(p => !p.IsActive).ToList();
                
                var categories = allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
                var activeCategories = activeProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
                
                var categoryProductCounts = categories.Select(cat => new
                {
                    Category = cat,
                    TotalProducts = allProducts.Count(p => p.Category == cat),
                    ActiveProducts = activeProducts.Count(p => p.Category == cat),
                    InactiveProducts = inactiveProducts.Count(p => p.Category == cat)
                }).ToList();

                var debugInfo = new
                {
                    TotalProducts = allProducts.Count,
                    ActiveProducts = activeProducts.Count,
                    InactiveProducts = inactiveProducts.Count,
                    TotalCategories = categories.Count,
                    ActiveCategories = activeCategories.Count,
                    CategoryList = categories, // Duplicate property ismini önle
                    ActiveCategoryList = activeCategories, // Duplicate property ismini önle
                    CategoryProductCounts = categoryProductCounts,
                    SampleProducts = activeProducts.Take(5).Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.IsActive,
                        p.Price
                    }).ToList()
                };

                _logger.LogInformation($"Debug info: {activeProducts.Count} active products in {activeCategories.Count} categories");
                return SuccessResponse(debugInfo, "Debug information retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetDebugCategoriesAndProducts");
            }
        }

        /// <summary>
        /// Tüm kategorileri getir
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetAllCategories()
        {
            try
            {
                var categories = await _context.Products
                    .Where(p => p.IsActive)
                    .Select(p => p.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                // İngilizce teknik log
                _logger.LogInformation($"Retrieved {categories.Count} unique categories");
                
                return SuccessResponse(categories, $"Retrieved {categories.Count} categories");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetAllCategories");
            }
        }

        /// <summary>
        /// Kategoriye göre ürünleri getir
        /// </summary>
        [HttpGet("category/{categoryName}")]
        public async Task<IActionResult> GetProductsByCategory(string categoryName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    return ErrorResponse("Category name cannot be empty", 400);
                }

                var products = await _context.Products
                    .Where(p => p.Category == categoryName && p.IsActive)
                    .OrderBy(p => p.Name)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Description,
                        p.Price,
                        p.ImageUrl,
                        p.StockQuantity,
                        p.TaxType,
                        p.TaxRate,
                        p.IsActive,
                        p.Category
                    })
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {products.Count} products for category: {categoryName}");
                return SuccessResponse(products, $"Retrieved {products.Count} products for category {categoryName}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetProductsByCategory");
            }
        }

        /// <summary>
        /// Stok durumuna göre ürünleri getir
        /// </summary>
        [HttpGet("stock/{status}")]
        public async Task<IActionResult> GetProductsByStockStatus(string status)
        {
            try
            {
                var products = status switch
                {
                    "in-stock" => await _context.Products
                        .Where(p => p.StockQuantity > p.MinStockLevel && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    
                    "out-of-stock" => await _context.Products
                        .Where(p => p.StockQuantity == 0 && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    
                    "low-stock" => await _context.Products
                        .Where(p => p.StockQuantity <= p.MinStockLevel && p.StockQuantity > 0 && p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToListAsync(),
                    
                    _ => throw new ArgumentException("Invalid stock status. Use: in-stock, out-of-stock, or low-stock")
                };

                _logger.LogInformation($"Retrieved {products.Count} products with stock status: {status}");
                return SuccessResponse(products, $"Retrieved {products.Count} products with {status} status");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetProductsByStockStatus");
            }
        }

        /// <summary>
        /// Ürün arama (çoklu kriter ile)
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string? name,
            [FromQuery] string? category)
        {
            try
            {
                var query = _context.Products.Where(p => p.IsActive);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    query = query.Where(p => p.Name.ToLower().Contains(name.ToLower()));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(p => p.Category.ToLower().Contains(category.ToLower()));
                }

                var products = await query.OrderBy(p => p.Name).ToListAsync();

                _logger.LogInformation($"Search completed. Found {products.Count} products matching criteria");
                return SuccessResponse(products, $"Found {products.Count} products matching search criteria");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "SearchProducts");
            }
        }

        /// <summary>
        /// Yeni ürün oluştur
        /// </summary>
        [HttpPost]
        public override async Task<IActionResult> Create([FromBody] Product product)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return ErrorResponse("Invalid product data", 400);
                }

                // RKSV validasyonu
                var validationResult = await ValidateProductForRKSVAsync(product);
                if (!validationResult.IsValid)
                {
                    return ErrorResponse(validationResult.ErrorMessage, 400);
                }

                // Audit alanları
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                product.CreatedBy = User.Identity?.Name ?? "system";
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.IsActive = true;

                var createdProduct = await _productRepository.AddAsync(product);

                _logger.LogInformation($"Product created successfully: {product.Name} (ID: {createdProduct.Id})");
                return SuccessResponse(createdProduct, "Product created successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create");
            }
        }

        /// <summary>
        /// Ürün güncelle
        /// </summary>
        [HttpPut("{id}")]
        public override async Task<IActionResult> Update(Guid id, [FromBody] Product product)
        {
            try
            {
                if (id != product.Id)
                {
                    return ErrorResponse("ID mismatch", 400);
                }

                if (!ModelState.IsValid)
                {
                    return ErrorResponse("Invalid product data", 400);
                }

                // RKSV validasyonu
                var validationResult = await ValidateProductForRKSVAsync(product);
                if (!validationResult.IsValid)
                {
                    return ErrorResponse(validationResult.ErrorMessage, 400);
                }

                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return ErrorResponse("Product not found", 404);
                }

                // Audit alanları
                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = User.Identity?.Name ?? "system";
                product.CreatedAt = existingProduct.CreatedAt;
                product.CreatedBy = existingProduct.CreatedBy;

                var updatedProduct = await _productRepository.UpdateAsync(product);

                _logger.LogInformation($"Product updated successfully: {product.Name} (ID: {id})");
                return SuccessResponse(updatedProduct, "Product updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Update");
            }
        }

        /// <summary>
        /// Ürün stok güncelle
        /// </summary>
        [HttpPut("stock/{id}")]
        public async Task<IActionResult> UpdateProductStock(Guid id, [FromBody] UpdateStockRequest request)
        {
            try
            {
                if (request.Quantity < 0)
                {
                    return ErrorResponse("Stock quantity cannot be negative", 400);
                }

                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return ErrorResponse("Product not found", 404);
                }

                // Transaction kullanarak stok güncelle
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var oldStock = product.StockQuantity;
                    product.StockQuantity = request.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = User.Identity?.Name ?? "system";

                    var updatedProduct = await _productRepository.UpdateAsync(product);

                    await transaction.CommitAsync();

                    _logger.LogInformation($"Product stock updated: {product.Name} (ID: {id}) - Old: {oldStock}, New: {request.Quantity}");
                    return SuccessResponse(updatedProduct, "Product stock updated successfully");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex, "UpdateProductStock");
            }
        }

        /// <summary>
        /// RKSV uyumlu ürün validasyonu
        /// </summary>
        private async Task<ValidationResult> ValidateProductForRKSVAsync(Product product)
        {
            // Fiyat kontrolü
            if (product.Price <= 0)
            {
                return ValidationResult.Error("Price must be greater than zero");
            }

            // Stok kontrolü
            if (product.StockQuantity < 0)
            {
                return ValidationResult.Error("Stock quantity cannot be negative");
            }

            // Minimum stok seviyesi kontrolü
            if (product.MinStockLevel < 0)
            {
                return ValidationResult.Error("Minimum stock level cannot be negative");
            }

            // Kategori kontrolü
            if (string.IsNullOrWhiteSpace(product.Category))
            {
                return ValidationResult.Error("Category is required");
            }

            // Vergi tipi kontrolü
            if (!TaxTypes.All.Contains(product.TaxType))
            {
                return ValidationResult.Error($"Tax type must be one of: {string.Join(", ", TaxTypes.All)}");
            }

            // RKSV Compliance Validations
            if (!product.IsFiscalCompliant)
            {
                return ValidationResult.Error("Product must be fiscally compliant for RKSV standards");
            }

            if (!product.IsTaxable && string.IsNullOrWhiteSpace(product.TaxExemptionReason))
            {
                return ValidationResult.Error("Tax exemption reason is required for non-taxable products");
            }

            if (!RksvProductTypes.IsValidRksvType(product.RksvProductType))
            {
                return ValidationResult.Error($"RKSV product type must be one of: {string.Join(", ", RksvProductTypes.All)}");
            }

            // Austrian VAT Rate Validation
            var expectedTaxRate = TaxTypes.GetTaxRate(product.TaxType);
            if (product.TaxRate != expectedTaxRate)
            {
                return ValidationResult.Error($"Tax rate mismatch: Expected {expectedTaxRate}% for {product.TaxType} tax type");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Sayfalama validasyonu
        /// </summary>
        private (int pageNumber, int pageSize) ValidatePagination(int pageNumber, int pageSize)
        {
            var validPageNumber = Math.Max(1, pageNumber);
            var validPageSize = Math.Clamp(pageSize, 1, 100);
            return (validPageNumber, validPageSize);
        }
    }

    /// <summary>
    /// Stok güncelleme request modeli
    /// </summary>
    public class UpdateStockRequest
    {
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Validasyon sonucu
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }

        private ValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static ValidationResult Success() => new(true);
        public static ValidationResult Error(string message) => new(false, message);
    }
}
