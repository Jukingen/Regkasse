using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.CategorySeed;
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
    private readonly IDemoProductImportImageService _importImageService;
    private readonly ILogger<DemoProductImportService> _logger;

    public DemoProductImportService(
        AppDbContext context,
        IWebHostEnvironment environment,
        IDemoProductImportImageService importImageService,
        ILogger<DemoProductImportService> logger)
    {
        _context = context;
        _environment = environment;
        _importImageService = importImageService;
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
                productCounts.GetValueOrDefault(c.Name),
                ResolveCatalogCategoryDefaultVat(c, demoData)))
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
        IProgress<DemoImportProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var demoData = await LoadDemoDataAsync(cancellationToken).ConfigureAwait(false);
        return await ImportDemoDataAsync(tenantId, demoData, request, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]> GetTemplateCsvAsync(CancellationToken cancellationToken = default)
    {
        var demoData = await LoadDemoDataAsync(cancellationToken).ConfigureAwait(false);
        var csv = DemoProductTemplateExporter.BuildCsv(demoData);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    public Task<DemoTemplateValidationResultDto> ValidateTemplateFileAsync(
        Stream stream,
        string fileName,
        int maxPreviewRows = 20,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (rows, parseError) = DemoProductTemplateFileParser.Parse(stream, fileName);
        var result = DemoProductTemplateValidator.Validate(rows, parseError, maxPreviewRows);
        return Task.FromResult(result);
    }

    public async Task<ImportResult> ImportFromTemplateFileAsync(
        Guid tenantId,
        Stream stream,
        string fileName,
        DemoImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var (rows, parseError) = DemoProductTemplateFileParser.Parse(stream, fileName);
        if (parseError != null)
        {
            return new ImportResult { Success = false, ErrorMessage = parseError };
        }

        var (demoData, buildError) = DemoProductTemplateValidator.BuildDemoData(rows);
        if (demoData == null)
        {
            return new ImportResult { Success = false, ErrorMessage = buildError ?? "Template validation failed." };
        }

        var importRequest = new DemoImportRequest
        {
            OverwriteExisting = request.OverwriteExisting,
            PriceAdjustmentMode = request.PriceAdjustmentMode,
            PriceAdjustmentPercent = request.PriceAdjustmentPercent,
            PriceRoundIncrement = request.PriceRoundIncrement,
            ImageMode = request.ImageMode,
            ProductOverrides = request.ProductOverrides,
        };

        return await ImportDemoDataAsync(tenantId, demoData, importRequest, progress: null, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ImportResult> ImportDemoDataAsync(
        Guid tenantId,
        DemoData demoData,
        DemoImportRequest request,
        IProgress<DemoImportProgressDto>? progress,
        CancellationToken cancellationToken)
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

            var categoriesToImport = DemoProductImportFilter.SelectCategories(demoData, request);
            result.SelectedCategoryCount = categoriesToImport.Count;

            if (categoriesToImport.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No demo categories matched the import selection.";
                return result;
            }

            var (categories, categoriesCreated) = await GetOrCreateCategoriesAsync(tenantId, categoriesToImport, cancellationToken)
                .ConfigureAwait(false);
            result.CategoriesCreated = categoriesCreated;
            importedCategoryIds.AddRange(categories.Values.Select(c => c.Id));

            var categoryLookup = categoriesToImport.ToDictionary(c => c.Name, StringComparer.Ordinal);
            var productsToImport = DemoProductImportFilter.SelectProducts(demoData, categoryLookup, request);
            result.TotalProductCount = productsToImport.Count;

            if (productsToImport.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No demo products matched the import selection.";
                ReportProgress(progress, DemoImportJobStatus.Failed, 0, 0, 0, 0, null, Array.Empty<DemoImportCategoryProgressDto>(), result.ErrorMessage);
                return result;
            }

            var categoryProgress = BuildInitialCategoryProgress(categoriesToImport, productsToImport);
            ReportProgress(
                progress,
                DemoImportJobStatus.Running,
                productsToImport.Count,
                0,
                0,
                0,
                null,
                categoryProgress);

            var categorySummaries = categoriesToImport.ToDictionary(
                c => c.Name,
                c => new CategoryImportSummary
                {
                    CategoryName = c.Name,
                    ProductCount = productsToImport.Count(p => p.Category == c.Name),
                },
                StringComparer.Ordinal);

            var imageMode = DemoProductImportImageModeParser.Parse(request.ImageMode);
            var overrideLookup = request.ProductOverrides
                .Where(o => o.CatalogProductId != Guid.Empty)
                .GroupBy(o => o.CatalogProductId)
                .ToDictionary(g => g.Key, g => g.First());

            var sequence = 0;
            var processedCount = 0;
            var importedCount = 0;
            var skippedCount = 0;
            decimal importedPriceSum = 0m;
            var importedPriceCount = 0;
            foreach (var demoProduct in productsToImport)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MarkCategoryProcessing(categoryProgress, demoProduct.Category);
                ReportProgress(
                    progress,
                    DemoImportJobStatus.Running,
                    productsToImport.Count,
                    processedCount,
                    importedCount,
                    skippedCount,
                    demoProduct.Name,
                    categoryProgress);

                if (!categories.TryGetValue(demoProduct.Category, out var category))
                {
                    _logger.LogWarning(
                        "Skipping demo product {ProductName}: unknown category {Category}",
                        demoProduct.Name,
                        demoProduct.Category);
                    processedCount++;
                    AdvanceCategoryProgress(categoryProgress, demoProduct.Category, imported: false, skipped: false);
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
                    processedCount++;
                    skippedCount++;
                    AdvanceCategoryProgress(categoryProgress, demoProduct.Category, imported: false, skipped: true);
                    ReportProgress(
                        progress,
                        DemoImportJobStatus.Running,
                        productsToImport.Count,
                        processedCount,
                        importedCount,
                        skippedCount,
                        demoProduct.Name,
                        categoryProgress);
                    continue;
                }

                var now = DateTime.UtcNow;
                var (importPrice, importTaxRate) = ResolveImportPriceAndTax(demoProduct, request, overrideLookup);
                var taxType = MapTaxType(importTaxRate);
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
                    IsTaxable = importTaxRate > 0,
                    RksvProductType = MapRksvProductType(taxType),
                    IsSellableAddOn = false,
                };

                product.NameDe = demoProduct.Name;
                product.NameEn = demoProduct.Name;
                product.NameTr = demoProduct.Name;
                product.DescriptionDe = demoProduct.Description;
                ProductLocalization.SyncCanonicalFields(product);
                product.Price = importPrice;
                product.TaxType = taxType;
                product.TaxRate = importTaxRate;
                product.IsTaxable = importTaxRate > 0;
                product.RksvProductType = MapRksvProductType(taxType);
                product.CategoryId = category.Id;
                product.Category = category.Name;
                product.IsActive = true;
                product.UpdatedAt = now;
                product.UpdatedBy = "demo-import";

                if (ShouldAssignImportImage(existingProduct, request))
                {
                    await _importImageService
                        .TryAssignPlaceholderAsync(tenantId, product, demoProduct.Category, imageMode, cancellationToken)
                        .ConfigureAwait(false);
                }

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
                importedPriceSum += importPrice;
                importedPriceCount++;
                processedCount++;
                importedCount++;
                AdvanceCategoryProgress(categoryProgress, demoProduct.Category, imported: true, skipped: false);
                ReportProgress(
                    progress,
                    DemoImportJobStatus.Running,
                    productsToImport.Count,
                    processedCount,
                    importedCount,
                    skippedCount,
                    demoProduct.Name,
                    categoryProgress);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            result.Success = true;
            result.ImportedProductCount = result.Created + result.Updated;
            result.AverageImportedPrice = importedPriceCount > 0
                ? Math.Round(importedPriceSum / importedPriceCount, 2, MidpointRounding.AwayFromZero)
                : 0m;
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
        if (data == null || data.Products.Count == 0)
            throw new InvalidOperationException("Demo product catalog is empty or invalid.");

        data.Categories = SystemCategories.CreateDemoCatalogCategories().ToList();
        SystemCategories.NormalizeProductReferences(data);
        DemoProductImportFilter.NormalizeDemoProductIds(data);
        return data;
    }

    private async Task<(Dictionary<string, Category> Categories, int CreatedCount)> GetOrCreateCategoriesAsync(
        Guid tenantId,
        List<DemoCategory> demoCategories,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Category>(StringComparer.Ordinal);
        var createdCount = 0;

        foreach (var demoCat in demoCategories)
        {
            var categoryKey = !string.IsNullOrWhiteSpace(demoCat.Key)
                ? demoCat.Key
                : CategoryKey.FromDisplayName(demoCat.Name);
            var category = await _context.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Key == categoryKey, cancellationToken)
                .ConfigureAwait(false)
                ?? await _context.Categories
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == demoCat.Name, cancellationToken)
                    .ConfigureAwait(false);

            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = categoryKey,
                    Name = demoCat.Name,
                    OriginalDemoName = demoCat.Name,
                    Description = demoCat.Description,
                    Icon = demoCat.Icon,
                    SortOrder = demoCat.SortOrder,
                    VatRate = demoCat.VatRate,
                    FiscalCategory = demoCat.FiscalCategory == RksvProductCategory.Unspecified
                        ? CategoryKey.InferFiscalCategory(demoCat.Name, demoCat.Description)
                        : demoCat.FiscalCategory,
                    IsSystemCategory = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "demo-import",
                };
                _context.Categories.Add(category);
                createdCount++;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(category.Key))
                    category.Key = categoryKey;

                if (string.IsNullOrWhiteSpace(category.OriginalDemoName))
                    category.OriginalDemoName = demoCat.Name;

                category.IsSystemCategory = true;

                if (string.IsNullOrWhiteSpace(category.Icon) && !string.IsNullOrWhiteSpace(demoCat.Icon))
                    category.Icon = demoCat.Icon;

                if (category.SortOrder != demoCat.SortOrder
                    || category.Description != demoCat.Description
                    || category.VatRate != demoCat.VatRate)
                {
                    category.SortOrder = demoCat.SortOrder;
                    category.Description = demoCat.Description;
                    category.VatRate = demoCat.VatRate;
                    category.UpdatedAt = DateTime.UtcNow;
                    category.UpdatedBy = "demo-import";
                }
            }

            result[demoCat.Name] = category;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (result, createdCount);
    }

    private static (decimal Price, decimal TaxRate) ResolveImportPriceAndTax(
        DemoProduct demoProduct,
        DemoImportRequest request,
        IReadOnlyDictionary<Guid, DemoImportProductOverrideDto> overrides)
    {
        var price = ApplyImportPrice(demoProduct.Price, request);
        var taxRate = demoProduct.TaxRate;

        if (overrides.TryGetValue(demoProduct.Id, out var ov))
        {
            if (ov.Price is >= 0)
                price = Math.Round(ov.Price.Value, 2, MidpointRounding.AwayFromZero);
            if (ov.TaxRate is >= 0)
                taxRate = ov.TaxRate.Value;
        }

        return (price, taxRate);
    }

    private static bool ShouldAssignImportImage(Product? existingProduct, DemoImportRequest request)
    {
        if (DemoProductImportImageModeParser.Parse(request.ImageMode) == DemoImportImageMode.None)
            return false;

        return existingProduct == null || request.OverwriteExisting;
    }

    private static decimal ApplyImportPrice(decimal catalogPrice, DemoImportRequest request)
    {
        var mode = DemoProductPriceAdjustment.ParseMode(request.PriceAdjustmentMode);
        var percent = request.PriceAdjustmentPercent ?? 0m;
        var increment = request.PriceRoundIncrement ?? 0.50m;
        return DemoProductPriceAdjustment.Apply(catalogPrice, mode, percent, increment);
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

    /// <summary>Default VAT for import validation UI (mode of product rates; drinks → 20%).</summary>
    private static decimal ResolveCatalogCategoryDefaultVat(DemoCategory category, DemoData data)
    {
        if (category.Name.Contains("Getrn", StringComparison.OrdinalIgnoreCase))
            return 20m;

        var products = data.Products.Where(p => p.Category == category.Name).ToList();
        if (products.Count == 0)
            return category.VatRate;

        return products
            .GroupBy(p => p.TaxRate)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .First()
            .Key;
    }

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

    private static List<DemoImportCategoryProgressDto> BuildInitialCategoryProgress(
        List<DemoCategory> categoriesToImport,
        List<DemoProduct> productsToImport)
    {
        var list = new List<DemoImportCategoryProgressDto>();
        foreach (var cat in categoriesToImport)
        {
            var total = productsToImport.Count(p => p.Category == cat.Name);
            if (total == 0)
                continue;

            list.Add(new DemoImportCategoryProgressDto(cat.Name, total, State: "Waiting"));
        }

        if (list.Count > 0)
            list[0] = list[0] with { State = "Processing" };

        return list;
    }

    private static void MarkCategoryProcessing(
        List<DemoImportCategoryProgressDto> categories,
        string categoryName)
    {
        for (var i = 0; i < categories.Count; i++)
        {
            var row = categories[i];
            if (row.CategoryName == categoryName)
            {
                categories[i] = row with { State = "Processing" };
            }
            else if (row.State == "Processing" && row.Processed < row.Total)
            {
                categories[i] = row with { State = "Waiting" };
            }
        }
    }

    private static void AdvanceCategoryProgress(
        List<DemoImportCategoryProgressDto> categories,
        string categoryName,
        bool imported,
        bool skipped)
    {
        for (var i = 0; i < categories.Count; i++)
        {
            var row = categories[i];
            if (row.CategoryName != categoryName)
                continue;

            var processed = row.Processed + 1;
            var importedCount = row.Imported + (imported ? 1 : 0);
            var skippedCount = row.Skipped + (skipped ? 1 : 0);
            var state = processed >= row.Total ? "Completed" : "Processing";
            categories[i] = row with
            {
                Processed = processed,
                Imported = importedCount,
                Skipped = skippedCount,
                State = state,
            };

            if (state == "Completed" && i + 1 < categories.Count)
                categories[i + 1] = categories[i + 1] with { State = "Processing" };

            break;
        }
    }

    private static void ReportProgress(
        IProgress<DemoImportProgressDto>? progress,
        DemoImportJobStatus status,
        int totalProducts,
        int processedProducts,
        int importedCount,
        int skippedCount,
        string? currentProductName,
        IReadOnlyList<DemoImportCategoryProgressDto> categories,
        string? message = null)
    {
        if (progress == null)
            return;

        var percent = totalProducts > 0
            ? (int)Math.Round(processedProducts * 100.0 / totalProducts, MidpointRounding.AwayFromZero)
            : 0;

        progress.Report(new DemoImportProgressDto(
            Status: status,
            TotalProducts: totalProducts,
            ProcessedProducts: processedProducts,
            ImportedCount: importedCount,
            SkippedCount: skippedCount,
            CurrentProductName: currentProductName,
            Percent: Math.Clamp(percent, 0, 100),
            Categories: categories,
            Message: message));
    }
}
