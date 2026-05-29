using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class DemoProductImportService : IDemoProductImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DemoProductImportService> _logger;

    public DemoProductImportService(
        AppDbContext context,
        IWebHostEnvironment environment,
        ILogger<DemoProductImportService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DemoImportCatalogDto> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var demoData = await LoadDemoDataAsync(cancellationToken).ConfigureAwait(false);
        var productCounts = demoData.Products
            .GroupBy(p => p.Category, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var categories = demoData.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new DemoImportCatalogCategoryDto(
                c.Name,
                c.Description,
                c.SortOrder,
                productCounts.GetValueOrDefault(c.Name)))
            .ToList();

        var products = demoData.Products
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new DemoImportCatalogProductDto(
                p.Id,
                p.Name,
                p.Category,
                p.Price,
                p.TaxRate))
            .ToList();

        return new DemoImportCatalogDto(categories, products);
    }

    public async Task<ImportResult> ImportDemoProductsAsync(
        Guid tenantId,
        DemoImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult();
        var importedCategoryIds = new List<Guid>();
        var importedProductIds = new List<Guid>();

        try
        {
            var tenantExists = await _context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (!tenantExists)
            {
                result.Success = false;
                result.ErrorMessage = "Tenant not found.";
                return result;
            }

            var demoData = await LoadDemoDataAsync(cancellationToken).ConfigureAwait(false);

            var categoriesToImport = DemoProductImportFilter.SelectCategories(demoData, request);
            result.SelectedCategoryCount = categoriesToImport.Count;

            if (categoriesToImport.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No demo categories matched the import selection.";
                return result;
            }

            var categories = await GetOrCreateCategoriesAsync(tenantId, categoriesToImport, cancellationToken)
                .ConfigureAwait(false);
            importedCategoryIds.AddRange(categories.Values.Select(c => c.Id));

            var categoryLookup = categoriesToImport.ToDictionary(c => c.Name, StringComparer.Ordinal);
            var productsToImport = DemoProductImportFilter.SelectProducts(demoData, categoryLookup, request);
            result.TotalProductCount = productsToImport.Count;

            if (productsToImport.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No demo products matched the import selection.";
                return result;
            }

            var categorySummaries = categoriesToImport.ToDictionary(
                c => c.Name,
                c => new CategoryImportSummary
                {
                    CategoryName = c.Name,
                    ProductCount = productsToImport.Count(p => p.Category == c.Name),
                },
                StringComparer.Ordinal);

            var sequence = 0;
            foreach (var demoProduct in productsToImport)
            {
                if (!categories.TryGetValue(demoProduct.Category, out var category))
                {
                    _logger.LogWarning(
                        "Skipping demo product {ProductName}: unknown category {Category}",
                        demoProduct.Name,
                        demoProduct.Category);
                    continue;
                }

                if (!categorySummaries.TryGetValue(demoProduct.Category, out var summary))
                {
                    summary = new CategoryImportSummary
                    {
                        CategoryName = demoProduct.Category,
                        ProductCount = productsToImport.Count(p => p.Category == demoProduct.Category),
                    };
                    categorySummaries[demoProduct.Category] = summary;
                }

                var existingProduct = await _context.Products
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        p => p.TenantId == tenantId && p.Name == demoProduct.Name,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existingProduct != null && !request.OverwriteExisting)
                {
                    result.Skipped++;
                    summary.Skipped++;
                    importedProductIds.Add(existingProduct.Id);
                    continue;
                }

                var now = DateTime.UtcNow;
                var taxType = MapTaxType(demoProduct.TaxRate);
                var product = existingProduct ?? new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CreatedAt = now,
                    CreatedBy = "demo-import",
                    StockQuantity = 100,
                    MinStockLevel = 0,
                    Unit = "Stk",
                    Cost = 0m,
                    Barcode = BuildBarcode(demoProduct.Name, ++sequence),
                    IsFiscalCompliant = true,
                    IsTaxable = demoProduct.TaxRate > 0,
                    RksvProductType = MapRksvProductType(taxType),
                    IsSellableAddOn = false,
                };

                product.Name = demoProduct.Name;
                product.Description = demoProduct.Description;
                product.Price = demoProduct.Price;
                product.TaxType = taxType;
                product.TaxRate = demoProduct.TaxRate;
                product.CategoryId = category.Id;
                product.Category = category.Name;
                product.IsActive = true;
                product.UpdatedAt = now;
                product.UpdatedBy = "demo-import";

                if (existingProduct == null)
                {
                    _context.Products.Add(product);
                    result.Created++;
                    summary.Created++;
                }
                else
                {
                    _context.Products.Update(product);
                    result.Updated++;
                }

                importedProductIds.Add(product.Id);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            result.Success = true;
            result.CategoryIds = importedCategoryIds.Distinct().ToList();
            result.ProductIds = importedProductIds;
            result.CategorySummaries = categorySummaries.Values
                .OrderBy(s => categoriesToImport.FindIndex(c => c.Name == s.CategoryName))
                .ToList();

            _logger.LogInformation(
                "Demo products imported for tenant {TenantId}: Created={Created}, Updated={Updated}, Skipped={Skipped}, SelectedCategories={SelectedCategoryCount}, TotalProducts={TotalProductCount}",
                tenantId,
                result.Created,
                result.Updated,
                result.Skipped,
                result.SelectedCategoryCount,
                result.TotalProductCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import demo products for tenant {TenantId}", tenantId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<DemoData> LoadDemoDataAsync(CancellationToken cancellationToken)
    {
        var jsonPath = Path.Combine(_environment.ContentRootPath, "Data", "demo-products.json");
        if (!File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "demo-products.json");
        }

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Demo product catalog not found at '{jsonPath}'.");

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
        var data = JsonSerializer.Deserialize<DemoData>(json, JsonOptions);
        if (data == null || data.Categories.Count == 0 || data.Products.Count == 0)
            throw new InvalidOperationException("Demo product catalog is empty or invalid.");

        DemoProductImportFilter.NormalizeDemoProductIds(data);
        return data;
    }

    private async Task<Dictionary<string, Category>> GetOrCreateCategoriesAsync(
        Guid tenantId,
        List<DemoCategory> demoCategories,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Category>(StringComparer.Ordinal);

        foreach (var demoCat in demoCategories)
        {
            var category = await _context.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == demoCat.Name, cancellationToken)
                .ConfigureAwait(false);

            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = demoCat.Name,
                    Description = demoCat.Description,
                    SortOrder = demoCat.SortOrder,
                    VatRate = demoCat.VatRate,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "demo-import",
                };
                _context.Categories.Add(category);
            }
            else if (category.SortOrder != demoCat.SortOrder || category.Description != demoCat.Description)
            {
                category.SortOrder = demoCat.SortOrder;
                category.Description = demoCat.Description;
                category.VatRate = demoCat.VatRate;
                category.UpdatedAt = DateTime.UtcNow;
                category.UpdatedBy = "demo-import";
            }

            result[demoCat.Name] = category;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static int MapTaxType(decimal taxRate) =>
        taxRate switch
        {
            0m => TaxTypes.ZeroRate,
            10m => TaxTypes.Reduced,
            13m => TaxTypes.Special,
            _ => TaxTypes.Standard,
        };

    private static string MapRksvProductType(int taxType) =>
        taxType switch
        {
            TaxTypes.Reduced => RksvProductTypes.Reduced,
            TaxTypes.Special => RksvProductTypes.Special,
            TaxTypes.ZeroRate => RksvProductTypes.Exempt,
            _ => RksvProductTypes.Standard,
        };

    private static string BuildBarcode(string productName, int sequence)
    {
        var slug = new string(productName
            .ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .Take(20)
            .ToArray());
        if (slug.Length == 0)
            slug = "ITEM";

        var barcode = $"DEMO-{slug}-{sequence:D3}";
        return barcode.Length <= 50 ? barcode : barcode[..50];
    }
}
